// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import es from "../../../i18n/locales/es.json";
import { authService } from "../api/authService";
import { ForgotPasswordPage } from "./ForgotPasswordPage";

vi.mock("../api/authService", () => ({
  authService: {
    forgotPassword: vi.fn(),
  },
}));

const neutralMessage = (es as Record<string, string>)["forgot.success"];

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ForgotPasswordPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

async function submit(email: string) {
  fireEvent.change(screen.getByLabelText(/correo|email/i), { target: { value: email } });
  fireEvent.click(screen.getByRole("button", { name: /enviar/i }));
  await waitFor(() => expect(screen.getByText(neutralMessage)).toBeTruthy());
}

describe("ForgotPasswordPage — respuesta neutral (ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01)", () => {
  afterEach(() => {
    cleanup();
    vi.mocked(authService.forgotPassword).mockReset();
  });

  it("muestra el mismo mensaje neutral para cualquier email aceptado", async () => {
    vi.mocked(authService.forgotPassword).mockResolvedValue(undefined);
    renderPage();

    await submit("existe@test.com");
    const first = screen.getByText(neutralMessage).textContent;

    await submit("no-existe@test.com");
    const second = screen.getByText(neutralMessage).textContent;

    expect(neutralMessage).toMatch(/si existe una cuenta/i);
    expect(second).toBe(first);
    expect(authService.forgotPassword).toHaveBeenNthCalledWith(1, "existe@test.com");
    expect(authService.forgotPassword).toHaveBeenNthCalledWith(2, "no-existe@test.com");
  });
});
