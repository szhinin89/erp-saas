import { Controller, type Control } from "react-hook-form";
import { ZHToggle } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { type BranchListItemDto } from "../../../branches/api/branchService";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";

interface Props {
  branchesCatalog: BranchListItemDto[];
  control: Control<UserConfigFormValues>;
  canManage: boolean;
  disabled: boolean;
  blockError: string;
}

/**
 * Sección "Acceso a sucursales" — parte del formulario único de UserConfigPage (Fase 2). Ya no
 * fetchea ni guarda por su cuenta: solo controla el campo `authorizedBranchIds` del formulario
 * compartido. En alta nueva arranca vacío (branchesCatalog completo, nada marcado); en edición
 * arranca con lo que trajo la carga inicial de la página.
 */
export function UserBranchesSection({
  branchesCatalog,
  control,
  canManage,
  disabled,
  blockError,
}: Props) {
  const { t } = useI18n();

  return (
    <div>
      {blockError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={blockError}
        />
      ) : null}

      <Controller
        name="authorizedBranchIds"
        control={control}
        render={({ field }) => (
          <div className="acc-branches-list">
            {branchesCatalog.length === 0 ? (
              <p className="subtle">
                {t(
                  "users.branches.empty",
                  "No hay sucursales activas en esta empresa.",
                )}
              </p>
            ) : (
              branchesCatalog.map((b) => {
                const checked = field.value.includes(b.id);
                return (
                  <ZHToggle
                    key={b.id}
                    label={b.name}
                    description={t(
                      "users.branches.toggleDescription",
                      "Autorizar acceso a esta sucursal",
                    )}
                    value={checked}
                    onChange={() => {
                      field.onChange(
                        checked
                          ? field.value.filter((id) => id !== b.id)
                          : [...field.value, b.id],
                      );
                    }}
                    disabled={disabled || !canManage}
                  />
                );
              })
            )}
          </div>
        )}
      />
    </div>
  );
}
