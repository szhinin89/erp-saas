import { describe, expect, it } from "vitest";
import {
  createFinancialDestinationSchema,
  editFinancialDestinationSchema,
  emptyCreateFinancialDestinationForm,
} from "./financialDestinationSchema";

describe("createFinancialDestinationSchema", () => {
  const bank = {
    ...emptyCreateFinancialDestinationForm(),
    code: "BANCO-001",
    name: "Cuenta corriente Pichincha",
    destinationTypeCode: "BankAccount" as const,
    accountingAccountId: "acc-1",
    bankInstitutionCode: "PICHINCHA",
    bankAccountIdentifierNormalized: "2200123456",
  };

  it("acepta un destino bancario completo", () => {
    expect(createFinancialDestinationSchema.safeParse(bank).success).toBe(true);
  });

  it("rechaza un destino bancario sin institución bancaria", () => {
    expect(
      createFinancialDestinationSchema.safeParse({ ...bank, bankInstitutionCode: "" })
        .success,
    ).toBe(false);
  });

  it("rechaza un destino bancario sin número de cuenta", () => {
    expect(
      createFinancialDestinationSchema.safeParse({
        ...bank,
        bankAccountIdentifierNormalized: "",
      }).success,
    ).toBe(false);
  });

  it("acepta un destino de caja con cashRegisterId", () => {
    const cash = {
      ...emptyCreateFinancialDestinationForm(),
      code: "CAJA-001",
      name: "Caja Matriz",
      destinationTypeCode: "CashRegister" as const,
      accountingAccountId: "acc-2",
      cashRegisterId: "cr-1",
    };
    expect(createFinancialDestinationSchema.safeParse(cash).success).toBe(true);
  });

  it("rechaza un destino de caja sin cashRegisterId", () => {
    const cash = {
      ...emptyCreateFinancialDestinationForm(),
      code: "CAJA-001",
      name: "Caja Matriz",
      destinationTypeCode: "CashRegister" as const,
      accountingAccountId: "acc-2",
    };
    expect(createFinancialDestinationSchema.safeParse(cash).success).toBe(false);
  });

  it("rechaza sin cuenta contable", () => {
    expect(
      createFinancialDestinationSchema.safeParse({ ...bank, accountingAccountId: "" })
        .success,
    ).toBe(false);
  });
});

describe("editFinancialDestinationSchema", () => {
  it("acepta nombre y cuenta contable válidos", () => {
    expect(
      editFinancialDestinationSchema.safeParse({
        name: "Cuenta corriente Pichincha (renombrada)",
        accountingAccountId: "acc-1",
      }).success,
    ).toBe(true);
  });

  it("rechaza sin cuenta contable", () => {
    expect(
      editFinancialDestinationSchema.safeParse({ name: "X", accountingAccountId: "" })
        .success,
    ).toBe(false);
  });
});
