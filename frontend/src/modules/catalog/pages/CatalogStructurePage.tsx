import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { CatalogStructureCascadePanel } from './CatalogStructureCascadePanel';
import { CatalogStructureModal } from './CatalogStructureModal';
import { useCatalogStructurePage } from './useCatalogStructurePage';

export function CatalogStructurePage() {
  const page = useCatalogStructurePage();

  if (!page.canView) return <NoAccessPage title={page.t('catalog.structure.title')} />;

  return (
    <div className="pg-page">
      <div className="pg-header-row">
        <div>
          <p className="pg-kicker">{page.t('app.nav.group.inventario')}</p>
          <h1 className="pg-title">{page.t('catalog.structure.title')}</h1>
          <p className="pg-subtitle">{page.t('catalog.structure.subtitle')}</p>
        </div>
      </div>

      {page.error && !page.modal && (
        <ZHPageNotice variant="error" message={page.t('common.errorPrefix')} detail={page.error} />
      )}

      <CatalogStructureCascadePanel
        t={page.t}
        lines={page.lines}
        categories={page.categories}
        subcategories={page.subcategories}
        linesLoading={page.linesLoading}
        catsLoading={page.catsLoading}
        subsLoading={page.subsLoading}
        selectedLineId={page.selectedLineId}
        selectedCategoryId={page.selectedCategoryId}
        selectedLine={page.selectedLine}
        selectedCategory={page.selectedCategory}
        saving={page.saving}
        canCreateLines={page.canCreateLines}
        canCreateCategories={page.canCreateCategories}
        canCreateSubcategories={page.canCreateSubcategories}
        canUpdateLines={page.canUpdateLines}
        canDeleteLines={page.canDeleteLines}
        canUpdateCategories={page.canUpdateCategories}
        canDeleteCategories={page.canDeleteCategories}
        canUpdateSubcategories={page.canUpdateSubcategories}
        canDeleteSubcategories={page.canDeleteSubcategories}
        onSelectLine={page.selectLine}
        onSelectCategory={page.selectCategory}
        onCreateLine={() => page.openCreate('create-line')}
        onCreateCategory={() => page.openCreate('create-category')}
        onCreateSubcategory={() => page.openCreate('create-subcategory')}
        onEditLine={(item) => page.openEdit(item, 'edit-line')}
        onEditCategory={(item) => page.openEdit(item, 'edit-category')}
        onEditSubcategory={(item) => page.openEdit(item, 'edit-subcategory')}
        onToggleLine={page.toggleLine}
        onToggleCategory={page.toggleCategory}
        onToggleSubcategory={page.toggleSubcategory}
      />

      {page.modal && (
        <CatalogStructureModal
          t={page.t}
          title={page.modalTitle()}
          saving={page.saving}
          error={page.error}
          showLineSelector={page.showLineSelector}
          showCategorySelector={page.showCategorySelector}
          lines={page.lines}
          modalCats={page.modalCats}
          watchedLineId={page.watchedLineId}
          register={page.register}
          setValue={page.setValue}
          errors={page.errors}
          onClose={page.closeModal}
          onSubmit={page.onSubmit}
        />
      )}
    </div>
  );
}
