const API_KEY_STORAGE = "zh-print-agent-api-key";
const app = document.getElementById("app");
const navTabs = document.getElementById("nav-tabs");

function getStoredKey() {
  try {
    return window.localStorage.getItem(API_KEY_STORAGE) || "";
  } catch {
    return "";
  }
}

function setStoredKey(key) {
  try {
    if (key) {
      window.localStorage.setItem(API_KEY_STORAGE, key);
    } else {
      window.localStorage.removeItem(API_KEY_STORAGE);
    }
  } catch {
    // localStorage unavailable (private mode, blocked storage) - key just won't persist across reloads.
  }
}

async function api(path, options = {}) {
  const headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});
  const key = getStoredKey();
  if (key) {
    headers["X-ZH-PrintAgent-Key"] = key;
  }

  const response = await fetch(path, Object.assign({}, options, { headers }));
  if (response.status === 401) {
    const err = new Error("unauthorized");
    err.unauthorized = true;
    throw err;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const err = new Error((data && (data.title || data.error)) || "Error");
    err.data = data;
    throw err;
  }
  return data;
}

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (key === "text") {
      node.textContent = value;
    } else if (key.startsWith("on")) {
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else {
      node.setAttribute(key, value);
    }
  }
  for (const child of [].concat(children)) {
    if (child) {
      node.appendChild(typeof child === "string" ? document.createTextNode(child) : child);
    }
  }
  return node;
}

function badge(text, kind) {
  return el("span", { class: `badge ${kind}`, text });
}

function message(text, kind) {
  return text ? el("div", { class: `message ${kind}` }, text) : null;
}

function setActiveTab(route) {
  navTabs.querySelectorAll("a").forEach((a) => {
    a.classList.toggle("active", a.dataset.route === route);
  });
  navTabs.style.display = route === "wizard" || route === "connect" ? "none" : "flex";
}

async function renderConnectScreen(errorText) {
  app.innerHTML = "";
  const input = el("input", { type: "text", placeholder: "API key" });
  app.appendChild(
    el("div", { class: "card" }, [
      el("h2", { text: "Conectar al Print Agent" }),
      message(errorText, "error"),
      el("p", { class: "hint", text: "Este equipo ya completó la configuración inicial. Ingresa la API key para administrar el agente." }),
      el("div", { class: "field" }, [el("label", { text: "API key" }), input]),
      el("button", {
        text: "Conectar",
        onclick: () => {
          setStoredKey(input.value.trim());
          route();
        }
      })
    ])
  );
}

async function loadStatus() {
  return api("/api/admin/status");
}

function statusRows(status) {
  const rows = [
    ["Servicio activo", status.active ? "Sí" : "No", status.active ? "ok" : "error"],
    ["Puerto", status.port, "muted"],
    ["Binding actual", status.bindHost, "muted"],
    ["Modo", status.mode === "lan" ? "LAN" : "Solo localhost", status.mode === "lan" ? "warn" : "ok"],
    ["API key configurada", status.apiKeyConfigured ? "Sí" : "No", status.apiKeyConfigured ? "ok" : "error"],
    ["Impresora por defecto", status.defaultPrinter || "(ninguna)", status.defaultPrinter ? "ok" : "warn"],
    ["Driver", status.driver || "-", "muted"],
    ["Salud", status.health, "ok"],
    ["Listo (ready)", status.ready ? "Sí" : "No", status.ready ? "ok" : "error"],
    ["Carpeta de datos", status.dataDirectory, "muted"],
    ["Carpeta de logs", status.logDirectory, "muted"]
  ];

  return el(
    "div",
    { class: "card" },
    [el("h2", { text: "Estado" })].concat(
      rows.map(([label, value, kind]) =>
        el("div", { class: "row" }, [
          el("span", { class: "label", text: label }),
          kind === "ok" || kind === "warn" || kind === "error"
            ? badge(String(value), kind)
            : el("span", { text: String(value) })
        ])
      )
    )
  );
}

async function renderStatus() {
  app.innerHTML = "";
  try {
    const status = await loadStatus();
    app.appendChild(statusRows(status));
    if (!status.ready && status.readinessErrors && status.readinessErrors.length) {
      app.appendChild(
        el(
          "div",
          { class: "card" },
          [el("h2", { text: "Diagnóstico" })].concat(
            status.readinessErrors.map((e) => el("div", { class: "message error", text: e }))
          )
        )
      );
    }
  } catch (e) {
    if (e.unauthorized) {
      return renderConnectScreen("");
    }
    app.appendChild(el("div", { class: "message error", text: "No se pudo obtener el estado: " + e.message }));
  }
}

async function renderPrinters() {
  app.innerHTML = "";
  let configured = [];
  let detected = [];
  try {
    [configured, detected] = await Promise.all([
      api("/api/admin/printers"),
      api("/api/admin/printers/windows")
    ]);
  } catch (e) {
    if (e.unauthorized) {
      return renderConnectScreen("");
    }
    app.appendChild(el("div", { class: "message error", text: "No se pudieron cargar las impresoras: " + e.message }));
    return;
  }

  const card = el("div", { class: "card" }, [el("h2", { text: "Impresoras configuradas" })]);
  const table = el("table", {}, [
    el("thead", {}, [
      el("tr", {}, [
        el("th", { text: "Nombre" }),
        el("th", { text: "Driver" }),
        el("th", { text: "Ancho" }),
        el("th", { text: "Habilitada" }),
        el("th", { text: "Predeterminada" }),
        el("th", { text: "Acciones" })
      ])
    ])
  ]);
  const tbody = el("tbody");
  table.appendChild(tbody);

  const state = configured.map((p) => Object.assign({}, p));

  function renderRows() {
    tbody.innerHTML = "";
    state.forEach((printer, index) => {
      const driverSelect = el(
        "select",
        { onchange: (ev) => (printer.driver = ev.target.value) },
        ["simulated", "windows-raw"].map((driver) =>
          el("option", { value: driver, selected: driver === printer.driver ? "selected" : null, text: driver })
        )
      );
      const widthSelect = el(
        "select",
        { onchange: (ev) => (printer.paperWidthMm = Number(ev.target.value)) },
        [80, 58].map((width) =>
          el("option", {
            value: String(width),
            selected: width === (printer.paperWidthMm || 80) ? "selected" : null,
            text: width + "mm"
          })
        )
      );
      const enabledCheck = el("input", { type: "checkbox" });
      enabledCheck.checked = printer.enabled !== false;
      enabledCheck.addEventListener("change", () => (printer.enabled = enabledCheck.checked));

      const defaultRadio = el("input", { type: "radio", name: "default-printer" });
      defaultRadio.checked = !!printer.isDefault;
      defaultRadio.addEventListener("change", () => {
        state.forEach((p) => (p.isDefault = false));
        printer.isDefault = true;
        renderRows();
      });

      tbody.appendChild(
        el("tr", {}, [
          el("td", { text: printer.name }),
          el("td", {}, driverSelect),
          el("td", {}, widthSelect),
          el("td", {}, enabledCheck),
          el("td", {}, defaultRadio),
          el(
            "td",
            { class: "actions" },
            el("button", {
              class: "secondary",
              text: "Imprimir prueba",
              onclick: () => testPrint(printer.name)
            })
          )
        ])
      );
    });
  }

  renderRows();
  card.appendChild(table);

  const feedback = el("div");
  card.appendChild(feedback);

  card.appendChild(
    el("div", { class: "actions" }, [
      el("button", {
        text: "Guardar impresoras",
        onclick: async () => {
          try {
            await api("/api/admin/printers", { method: "PUT", body: JSON.stringify(state) });
            feedback.innerHTML = "";
            feedback.appendChild(message("Guardado.", "ok"));
          } catch (e) {
            feedback.innerHTML = "";
            feedback.appendChild(message("No se pudo guardar: " + e.message, "error"));
          }
        }
      })
    ])
  );

  app.appendChild(card);

  const detectedCard = el("div", { class: "card" }, [
    el("h2", { text: "Impresoras Windows detectadas" }),
    el("p", { class: "hint", text: "Haz clic en \"Agregar\" para incorporarla a la lista de impresoras configuradas." })
  ]);
  const detectedTable = el("table", {}, [
    el("thead", {}, el("tr", {}, [el("th", { text: "Nombre" }), el("th", { text: "Predeterminada Windows" }), el("th", {})])),
    el(
      "tbody",
      {},
      detected.map((printer) =>
        el("tr", {}, [
          el("td", { text: printer.name }),
          el("td", { text: printer.isWindowsDefault ? "Sí" : "" }),
          el(
            "td",
            {},
            el("button", {
              class: "secondary",
              text: "Agregar",
              onclick: () => {
                if (!state.some((p) => p.name === printer.name)) {
                  state.push({
                    name: printer.name,
                    driver: "windows-raw",
                    enabled: true,
                    isDefault: state.length === 0,
                    paperWidthMm: 80
                  });
                  renderRows();
                }
              }
            })
          )
        ])
      )
    )
  ]);
  detectedCard.appendChild(detectedTable);
  app.appendChild(detectedCard);

  async function testPrint(name) {
    try {
      await api(`/api/admin/printers/${encodeURIComponent(name)}/test-print`, { method: "POST" });
      feedback.innerHTML = "";
      feedback.appendChild(message(`Trabajo de prueba enviado a '${name}'.`, "ok"));
    } catch (e) {
      feedback.innerHTML = "";
      feedback.appendChild(message("Fallo el test de impresión: " + e.message, "error"));
    }
  }
}

const STATUS_LABEL = {
  Pending: ["Pendiente", "muted"],
  Processing: ["Procesando", "warn"],
  Printed: ["Impreso", "ok"],
  Failed: ["Fallido", "error"],
  Cancelled: ["Cancelado", "muted"],
  NeedsReview: ["Requiere revisión", "error"]
};

async function renderQueue() {
  app.innerHTML = "";
  let data;
  try {
    data = await api("/api/admin/queue");
  } catch (e) {
    if (e.unauthorized) {
      return renderConnectScreen("");
    }
    app.appendChild(el("div", { class: "message error", text: "No se pudo cargar la cola: " + e.message }));
    return;
  }

  const summaryCard = el("div", { class: "card" }, [el("h2", { text: "Resumen" })]);
  Object.entries(data.counts || {}).forEach(([status, count]) => {
    const [label, kind] = STATUS_LABEL[status] || [status, "muted"];
    summaryCard.appendChild(
      el("div", { class: "row" }, [el("span", { class: "label", text: label }), badge(String(count), kind)])
    );
  });
  app.appendChild(summaryCard);

  const tableCard = el("div", { class: "card" }, [el("h2", { text: "Trabajos" })]);
  const table = el("table", {}, [
    el(
      "thead",
      {},
      el("tr", {}, [
        el("th", { text: "Job" }),
        el("th", { text: "Impresora" }),
        el("th", { text: "Estado" }),
        el("th", { text: "Intentos" }),
        el("th", { text: "Último error" }),
        el("th", { text: "Acciones" })
      ])
    )
  ]);
  const tbody = el("tbody");
  table.appendChild(tbody);

  function actionsFor(job) {
    const buttons = [];
    if (job.status === "Failed" || job.status === "NeedsReview") {
      buttons.push(
        el("button", { class: "secondary", text: "Reintentar", onclick: () => act(job.jobId, "retry") }),
        el("button", { class: "secondary", text: "Marcar revisado", onclick: () => act(job.jobId, "mark-reviewed") })
      );
    }
    if (job.status !== "Printed" && job.status !== "Cancelled") {
      buttons.push(el("button", { class: "danger", text: "Cancelar", onclick: () => act(job.jobId, "cancel") }));
    }
    return el("td", { class: "actions" }, buttons);
  }

  async function act(jobId, action) {
    try {
      await api(`/api/admin/queue/${encodeURIComponent(jobId)}/${action}`, { method: "POST" });
      renderQueue();
    } catch (e) {
      alert("No se pudo completar la acción: " + e.message);
    }
  }

  (data.items || []).forEach((job) => {
    const [label, kind] = STATUS_LABEL[job.status] || [job.status, "muted"];
    tbody.appendChild(
      el("tr", {}, [
        el("td", { text: job.jobId }),
        el("td", { text: job.printerName }),
        el("td", {}, badge(label, kind)),
        el("td", { text: String(job.attempts) }),
        el("td", { text: job.lastError || "" }),
        actionsFor(job)
      ])
    );
  });

  tableCard.appendChild(table);
  app.appendChild(tableCard);
}

function wizardStepIndicator(current, total) {
  const steps = [];
  for (let i = 1; i <= total; i++) {
    steps.push(el("span", { class: i === current ? "current" : "", text: `${i}` }));
  }
  return el("div", { class: "wizard-steps" }, steps);
}

async function renderWizard() {
  app.innerHTML = "";
  let status;
  try {
    status = await loadStatus();
  } catch (e) {
    app.appendChild(el("div", { class: "message error", text: "No se pudo iniciar el asistente: " + e.message }));
    return;
  }

  const card = el("div", { class: "card" });
  card.appendChild(el("h2", { text: "Configuración inicial" }));
  card.appendChild(wizardStepIndicator(1, 5));

  const body = el("div");
  card.appendChild(body);
  app.appendChild(card);

  body.appendChild(
    el("div", {}, [
      el("p", {}, `Servicio activo: ${status.active ? "sí" : "no"}. Binding: ${status.bindHost}:${status.port}.`),
      el("p", { class: "hint", text: "El siguiente paso genera la API key que usará este equipo de caja." }),
      el("button", { text: "Generar API key", onclick: generateKey })
    ])
  );

  async function generateKey() {
    try {
      const result = await api("/api/admin/apikey/regenerate", { method: "POST" });
      setStoredKey(result.apiKey);
      body.innerHTML = "";
      body.appendChild(wizardStepIndicator(2, 5));
      body.appendChild(
        el("div", {}, [
          el("p", { text: "Copia esta API key en un lugar seguro. No se volverá a mostrar completa salvo que la regeneres." }),
          el("div", { class: "key-box", text: result.apiKey }),
          el("button", { text: "Continuar", onclick: choosePrinter })
        ])
      );
    } catch (e) {
      body.appendChild(message("No se pudo generar la API key: " + e.message, "error"));
    }
  }

  async function choosePrinter() {
    body.innerHTML = "";
    body.appendChild(wizardStepIndicator(3, 5));
    let detected = [];
    try {
      detected = await api("/api/admin/printers/windows");
    } catch (e) {
      body.appendChild(message("No se pudieron listar las impresoras: " + e.message, "error"));
      return;
    }

    if (detected.length === 0) {
      body.appendChild(message("No se detectaron impresoras Windows en este equipo. Instala/conecta la impresora y recarga.", "error"));
      body.appendChild(el("button", { class: "secondary", text: "Reintentar", onclick: choosePrinter }));
      return;
    }

    const select = el(
      "select",
      {},
      detected.map((p) => el("option", { value: p.name, text: p.name + (p.isWindowsDefault ? " (predeterminada)" : "") }))
    );
    const driverSelect = el(
      "select",
      {},
      ["windows-raw", "simulated"].map((d) => el("option", { value: d, text: d }))
    );
    const widthSelect = el(
      "select",
      {},
      [80, 58].map((w) => el("option", { value: String(w), text: w + "mm" }))
    );

    body.appendChild(
      el("div", {}, [
        el("div", { class: "field" }, [el("label", { text: "Impresora" }), select]),
        el("div", { class: "field" }, [el("label", { text: "Driver" }), driverSelect]),
        el("div", { class: "field" }, [el("label", { text: "Ancho de papel" }), widthSelect]),
        el("button", {
          text: "Guardar y probar",
          onclick: () => savePrinterAndTest(select.value, driverSelect.value, Number(widthSelect.value))
        })
      ])
    );
  }

  async function savePrinterAndTest(name, driver, paperWidthMm) {
    body.innerHTML = "";
    body.appendChild(wizardStepIndicator(4, 5));
    try {
      await api("/api/admin/printers", {
        method: "PUT",
        body: JSON.stringify([{ name, driver, enabled: true, isDefault: true, paperWidthMm }])
      });
      await api(`/api/admin/printers/${encodeURIComponent(name)}/test-print`, { method: "POST" });
      body.appendChild(message(`Impresora guardada y prueba enviada a '${name}'.`, "ok"));
      body.appendChild(el("button", { text: "Finalizar configuración", onclick: completeSetup }));
    } catch (e) {
      body.appendChild(message("No se pudo guardar/probar la impresora: " + e.message, "error"));
    }
  }

  async function completeSetup() {
    body.innerHTML = "";
    body.appendChild(wizardStepIndicator(5, 5));
    try {
      await api("/api/admin/setup/complete", { method: "POST" });
      body.appendChild(message("Configuración completada. A partir de ahora se requiere la API key para administrar el agente.", "ok"));
      body.appendChild(el("button", { text: "Ir al estado", onclick: () => (window.location.hash = "#/status") }));
    } catch (e) {
      const errors = (e.data && e.data.errors && e.data.errors.setup) || [e.message];
      errors.forEach((err) => body.appendChild(message(err, "error")));
    }
  }
}

async function route() {
  const hash = window.location.hash.replace("#/", "") || "";

  let status;
  try {
    status = await loadStatus();
  } catch (e) {
    if (e.unauthorized) {
      setActiveTab("connect");
      return renderConnectScreen("");
    }
    setActiveTab("");
    app.innerHTML = "";
    app.appendChild(el("div", { class: "message error", text: "No se pudo contactar al agente: " + e.message }));
    return;
  }

  if (!status.setupCompleted && hash !== "wizard") {
    window.location.hash = "#/wizard";
    return;
  }

  const target = status.setupCompleted ? hash || "status" : "wizard";
  setActiveTab(target);

  if (target === "wizard") {
    return renderWizard();
  }
  if (target === "printers") {
    return renderPrinters();
  }
  if (target === "queue") {
    return renderQueue();
  }
  return renderStatus();
}

window.addEventListener("hashchange", route);
route();
