import { test } from "@playwright/test";
import { setupOperationalData } from "./setup-operational-data";

test("prepara datos operativos E2E", async () => {
  await setupOperationalData();
});
