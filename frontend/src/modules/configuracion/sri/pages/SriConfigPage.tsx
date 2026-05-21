import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { SriConfigPageDataTab } from './SriConfigPageDataTab';
import { useSriConfigPage } from './useSriConfigPage';

export function SriConfigPage() {
  const page = useSriConfigPage();
  const { register, control, setValue } = page.form;

  if (!page.canView) return <NoAccessPage title="Configuración SRI" />;
  if (page.sriState.loading) return <LoadingState />;

  return (
    <div className="pg-page">
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Configuración</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Facturación Electrónica SRI</span>
          </nav>
          <h1 className="pg-title">Configuración SRI</h1>
          <p className="pg-subtitle">
            Parámetros obligatorios para emitir comprobantes electrónicos según la ficha técnica del SRI Ecuador.
          </p>
        </div>
        {page.sriState.data && (
          <div className="pg-header-right">
            <span className={`badge badge--md ${page.sriState.data.environment === 1 ? 'badge--green' : 'badge--orange'}`}>
              {page.sriState.data.environment === 1 ? 'Producción' : 'Pruebas'}
            </span>
          </div>
        )}
      </div>

      {!page.sriState.data && !page.sriState.loading && (
        <ZHPageNotice
          variant="warning"
          message="Esta empresa no tiene configuración SRI todavía."
          detail="Completa el formulario para habilitar la facturación electrónica."
        />
      )}
      {page.sriState.error && (
        <ZHPageNotice variant="error" message="Error al cargar la configuración SRI." detail={page.sriState.error} />
      )}
      {page.saveError && <ZHPageNotice variant="error" message="Error al guardar." detail={page.saveError} />}
      {page.saved && <ZHPageNotice variant="success" message="Configuración SRI guardada correctamente." />}

      <form onSubmit={page.onSubmit}>
        <SriConfigPageDataTab
          register={register}
          control={control}
          errors={page.errors}
          saving={page.saving}
          canEdit={page.canEdit}
          showPass={page.showPass}
          setShowPass={page.setShowPass}
          hasExistingConfig={!!page.sriState.data}
          currentSequential={page.sriState.data?.currentSequential}
          setWsdlUrl={(url) => setValue('wsdlUrl', url, { shouldDirty: true })}
        />

        <div className="pg-actions-bar">
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            La contraseña del certificado se almacena cifrada. El secuencial no se resetea al actualizar.
          </div>
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={page.saving || !page.isDirty} onClick={page.handleDiscard}>
              Descartar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={page.saving || !page.canEdit || !page.isDirty}>
              <span className="material-symbols-outlined">save</span>
              {page.saving ? 'Guardando…' : 'Guardar Configuración SRI'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
