import { describe, expect, it } from "vitest";
import { userConfigSchema } from "../../schemas/access/userConfigSchema";
import { membershipRoleLabel } from "./roleLabels";
import {
  isMassAssignment,
  permStateDiff,
  summarizeGrants,
} from "./permissionsAssignmentState";
import type { PermissionCatalog } from "./api/adminPermissionsService";

/** IAM-V1-F2 — reglas puras de presentación y validación de Perfiles/Permisos/Usuarios. */
const t = (_key: string, fallback?: string) => fallback ?? _key;

const base = {
  username: "nuevo",
  firstName: "Nuevo",
  lastName: "Usuario",
  email: "",
  password: "Passw0rd!",
  authorizedBranchIds: [] as string[],
  loginMode: "AskBranch" as const,
  defaultBranchId: null,
};

const profileErrors = (values: Record<string, unknown>) => {
  const result = userConfigSchema.safeParse({ ...base, ...values });
  return result.success ? [] : result.error.issues.filter((i) => i.path[0] === "profileId");
};

describe("perfil obligatorio para asignaciones operativas nuevas", () => {
  it("alta o vinculación de un usuario operativo sin perfil es inválida", () => {
    expect(profileErrors({ stage: "create", role: "User", profileId: null })).toHaveLength(1);
    expect(profileErrors({ stage: "assign", role: "User", profileId: "" })).toHaveLength(1);
  });

  it("con perfil, o como Administrador de empresa, es válida", () => {
    expect(profileErrors({ stage: "create", role: "User", profileId: "p-1" })).toHaveLength(0);
    expect(profileErrors({ stage: "assign", role: "Admin", profileId: null })).toHaveLength(0);
  });

  it("editar un operativo existente sin perfil se conserva", () => {
    expect(profileErrors({ stage: "edit", role: "User", profileId: null })).toHaveLength(0);
  });
});

describe("etiqueta de la función", () => {
  it("Admin se presenta como Administrador de empresa; cualquier otro, Usuario operativo", () => {
    expect(membershipRoleLabel("Admin", t)).toBe("Administrador de empresa");
    expect(membershipRoleLabel("User", t)).toBe("Usuario operativo");
    expect(membershipRoleLabel(undefined, t)).toBe("Usuario operativo");
  });
});

const catalog: PermissionCatalog = {
  groups: [
    {
      code: "sales",
      labelKey: "Ventas",
      sortOrder: 1,
      categories: [
        {
          id: "c1",
          labelKey: "Operación",
          sortOrder: 1,
          items: [
            {
              id: "i1",
              labelKey: "Facturas",
              route: "/sales",
              permission: "sales.view",
              sortOrder: 1,
              actions: [
                { code: "sales.view", label: "Ver / Acceder", description: "", sortOrder: 0 },
                { code: "sales.create", label: "Crear", description: "", sortOrder: 1 },
              ],
            },
            {
              id: "i2",
              labelKey: "Devoluciones",
              route: "/sales/returns",
              permission: "sales.returns.view",
              sortOrder: 2,
              actions: [
                { code: "sales.returns.view", label: "Ver / Acceder", description: "", sortOrder: 0 },
              ],
            },
          ],
        },
      ],
    },
  ],
};

describe("resumen de lo que concede un perfil", () => {
  it("agrupa por módulo solo las pantallas con acceso y lista sus acciones concedidas", () => {
    const granted = new Set(["sales.view", "sales.create"]);
    const summary = summarizeGrants(catalog, (c) => granted.has(c));
    expect(summary.screenCount).toBe(1);
    expect(summary.actionCount).toBe(2);
    expect(summary.totalActionCount).toBe(3);
    expect(summary.groups).toEqual([
      {
        code: "sales",
        labelKey: "Ventas",
        screens: [{ id: "i1", labelKey: "Facturas", actionLabels: ["Ver / Acceder", "Crear"] }],
      },
    ]);
  });

  it("una acción sin el acceso de su pantalla no cuenta como pantalla concedida", () => {
    const summary = summarizeGrants(catalog, (c) => c === "sales.create");
    expect(summary.screenCount).toBe(0);
    expect(summary.groups).toEqual([]);
  });
});

describe("asignación masiva y diferencias", () => {
  it("más de la mitad del catálogo es asignación masiva", () => {
    expect(isMassAssignment(51, 100)).toBe(true);
    expect(isMassAssignment(50, 100)).toBe(false);
    expect(isMassAssignment(0, 0)).toBe(false);
  });

  it("cuenta lo que se agrega y lo que se quita respecto de lo guardado", () => {
    expect(
      permStateDiff(
        { a: true, b: false, c: true },
        { a: true, b: true, c: false },
        ["a", "b", "c"],
      ),
    ).toEqual({ added: 1, removed: 1 });
  });
});
