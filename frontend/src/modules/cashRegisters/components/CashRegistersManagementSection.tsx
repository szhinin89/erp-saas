import { useState } from 'react';
import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ConfigTabsLayout } from '../../../components/shared/ConfigTabsLayout';
import { useCashRegistersPage } from '../hooks/useCashRegistersPage';
import { CashRegistersListSection } from './CashRegistersListSection';
import { CashRegistersFormPanel } from './CashRegistersFormPanel';
import type { CashRegisterDto } from '../../caja/api/cajaService';
import '../../../styles/shared/items-catalog.css';

export function CashRegistersManagementSection() {
  const ctx = useCashRegistersPage();
  const [activeTab, setActiveTab] = useState<'list' | 'editor'>('list');

  const handleOpenCreate = async () => {
    await ctx.openCreate();
    setActiveTab('editor');
  };

  const handleOpenEdit = async (item: CashRegisterDto) => {
    await ctx.openEdit(item);
    setActiveTab('editor');
  };

  const handleCancel = () => {
    ctx.closePanel();
    setActiveTab('list');
  };

  if (!ctx.canView) return <NoAccessPage title="Administración de Cajas" />;

  const editorLabel = ctx.editingId ? 'Editar' : 'Nueva Caja';
  const editorIcon = ctx.editingId ? 'edit' : 'add_box';

  return (
    <ConfigTabsLayout
      activeTab={activeTab}
      onTabChange={setActiveTab}
      editorLabel={editorLabel}
      editorIcon={editorIcon}
      error={
        ctx.error
          ? <ZHPageNotice variant="error" message="Error:" detail={ctx.error} />
          : undefined
      }
      listContent={
        <CashRegistersListSection
          loading={ctx.loading}
          items={ctx.items}
          totals={ctx.totals}
          search={ctx.search}
          setSearch={ctx.setSearch}
          filtered={ctx.filtered}
          canManage={ctx.canManage}
          selectedId={ctx.selectedId}
          openCreate={handleOpenCreate}
          openEdit={handleOpenEdit}
          toggleDisable={ctx.toggleDisable}
          fetchList={ctx.fetchList}
        />
      }
      editorContent={
        ctx.panelOpen ? (
          <CashRegistersFormPanel
            editingId={ctx.editingId}
            editingCode={ctx.editingCode}
            editingName={ctx.editingName}
            editingHasHistory={ctx.editingHasHistory}
            saving={ctx.saving}
            saveError={ctx.saveError}
            branches={ctx.branches}
            loadingBranches={ctx.loadingBranches}
            emissionPoints={ctx.emissionPoints}
            loadingEmissionPoints={ctx.loadingEmissionPoints}
            derivedEstablishmentCode={ctx.derivedEstablishmentCode}
            warehouses={ctx.warehouses}
            loadingWarehouses={ctx.loadingWarehouses}
            register={ctx.register}
            control={ctx.control}
            setValue={ctx.setValue}
            watch={ctx.watch}
            errors={ctx.errors}
            closePanel={handleCancel}
            save={ctx.save}
          />
        ) : (
          <div className="cfg-tabs-empty">
            <span className="material-symbols-outlined cfg-empty-panel__icon">point_of_sale</span>
            <p className="cfg-empty-panel__title">Seleccione o cree una caja</p>
            <p className="cfg-empty-panel__sub">
              Use el botón <strong>Nueva Caja</strong> en la pestaña Lista, o seleccione una para editar.
            </p>
          </div>
        )
      }
    />
  );
}
