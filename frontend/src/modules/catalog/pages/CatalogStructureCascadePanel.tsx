import type {
  CatalogItem,
  ProductCategoryListItem,
  ProductSubcategoryListItem,
} from '../api/catalogService';
import { CascadeColumn, CascadeItem } from './CatalogStructureCascadeComponents';
import './CatalogStructurePage.css';

type CatalogStructureCascadePanelProps = {
  t: (key: string) => string;
  lines: CatalogItem[];
  categories: ProductCategoryListItem[];
  subcategories: ProductSubcategoryListItem[];
  linesLoading: boolean;
  catsLoading: boolean;
  subsLoading: boolean;
  selectedLineId: string | null;
  selectedCategoryId: string | null;
  selectedLine: CatalogItem | null;
  selectedCategory: ProductCategoryListItem | null;
  saving: boolean;
  canCreateLines: boolean;
  canCreateCategories: boolean;
  canCreateSubcategories: boolean;
  canUpdateLines: boolean;
  canDeleteLines: boolean;
  canUpdateCategories: boolean;
  canDeleteCategories: boolean;
  canUpdateSubcategories: boolean;
  canDeleteSubcategories: boolean;
  onSelectLine: (id: string) => void;
  onSelectCategory: (id: string) => void;
  onCreateLine: () => void;
  onCreateCategory: () => void;
  onCreateSubcategory: () => void;
  onEditLine: (item: CatalogItem) => void;
  onEditCategory: (item: ProductCategoryListItem) => void;
  onEditSubcategory: (item: ProductSubcategoryListItem) => void;
  onToggleLine: (item: CatalogItem) => void;
  onToggleCategory: (item: ProductCategoryListItem) => void;
  onToggleSubcategory: (item: ProductSubcategoryListItem) => void;
};

export function CatalogStructureCascadePanel({
  t,
  lines,
  categories,
  subcategories,
  linesLoading,
  catsLoading,
  subsLoading,
  selectedLineId,
  selectedCategoryId,
  selectedLine,
  selectedCategory,
  saving,
  canCreateLines,
  canCreateCategories,
  canCreateSubcategories,
  canUpdateLines,
  canDeleteLines,
  canUpdateCategories,
  canDeleteCategories,
  canUpdateSubcategories,
  canDeleteSubcategories,
  onSelectLine,
  onSelectCategory,
  onCreateLine,
  onCreateCategory,
  onCreateSubcategory,
  onEditLine,
  onEditCategory,
  onEditSubcategory,
  onToggleLine,
  onToggleCategory,
  onToggleSubcategory,
}: CatalogStructureCascadePanelProps) {
  return (
    <>
      <div className="cat-cascade-grid">
        <CascadeColumn
          icon="account_tree"
          title={t('catalog.structure.level1')}
          loading={linesLoading}
          empty={lines.length === 0}
          canCreate={canCreateLines}
          onAdd={onCreateLine}
        >
          {lines.map((line) => (
            <CascadeItem
              key={line.id}
              id={line.id}
              icon="folder"
              name={line.name}
              subtitle={`${line.code}`}
              isSelected={selectedLineId === line.id}
              isActive={line.isActive}
              onSelect={() => onSelectLine(line.id)}
              canEdit={canUpdateLines}
              canToggle={canDeleteLines || canUpdateLines}
              onEdit={() => onEditLine(line)}
              onToggle={() => void onToggleLine(line)}
              disabled={saving}
            />
          ))}
        </CascadeColumn>

        <CascadeColumn
          icon="category"
          title={t('catalog.structure.level2')}
          filterLabel={selectedLine ? `${t('catalog.structure.filteringBy')}: ${selectedLine.name}` : undefined}
          loading={catsLoading}
          empty={!selectedLineId ? true : categories.length === 0}
          canCreate={canCreateCategories && !!selectedLineId}
          onAdd={onCreateCategory}
        >
          {!selectedLineId ? (
            <p className="subtle cat-cascade-empty">{t('catalog.structure.selectLineFirst')}</p>
          ) : (
            categories.map((cat) => (
              <CascadeItem
                key={cat.id}
                id={cat.id}
                icon="list"
                name={cat.name}
                subtitle={`${cat.code}`}
                isSelected={selectedCategoryId === cat.id}
                isActive={cat.isActive}
                onSelect={() => onSelectCategory(cat.id)}
                canEdit={canUpdateCategories}
                canToggle={canDeleteCategories || canUpdateCategories}
                onEdit={() => onEditCategory(cat)}
                onToggle={() => void onToggleCategory(cat)}
                disabled={saving}
              />
            ))
          )}
        </CascadeColumn>

        <CascadeColumn
          icon="layers"
          title={t('catalog.structure.level3')}
          filterLabel={selectedCategory ? `${t('catalog.structure.filteringBy')}: ${selectedCategory.name}` : undefined}
          loading={subsLoading}
          empty={!selectedCategoryId ? true : subcategories.length === 0}
          canCreate={canCreateSubcategories && !!selectedCategoryId}
          onAdd={onCreateSubcategory}
        >
          {!selectedCategoryId ? (
            <p className="subtle cat-cascade-empty">{t('catalog.structure.selectCategoryFirst')}</p>
          ) : (
            subcategories.map((sub) => (
              <CascadeItem
                key={sub.id}
                id={sub.id}
                icon="subdirectory_arrow_right"
                name={sub.name}
                subtitle={`${sub.code} · ${sub.isActive ? t('common.active') : t('common.inactive')}`}
                isSelected={false}
                isActive={sub.isActive}
                onSelect={() => {}}
                canEdit={canUpdateSubcategories}
                canToggle={canDeleteSubcategories || canUpdateSubcategories}
                onEdit={() => onEditSubcategory(sub)}
                onToggle={() => void onToggleSubcategory(sub)}
                disabled={saving}
              />
            ))
          )}
        </CascadeColumn>
      </div>

      {(selectedLine || selectedCategory) && (
        <div className="pg-section cat-summary-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">info</span>
              <span className="pg-section-label">{t('catalog.structure.selectionSummary')}</span>
            </div>
          </div>
          <div className="cat-summary-row">
            <div>
              <p className="subtle cat-summary-label">{t('catalog.structure.hierarchyPath')}</p>
              <div className="cat-summary-path">
                {selectedLine && <span>{selectedLine.name}</span>}
                {selectedCategory && (
                  <>
                    <span className="material-symbols-outlined pg-icon-14 pg-cell-muted">arrow_forward</span>
                    <span className="cat-summary-path-primary">{selectedCategory.name}</span>
                  </>
                )}
              </div>
            </div>
            {selectedLineId && (
              <>
                <div className="cat-summary-divider" aria-hidden />
                <div>
                  <p className="subtle cat-summary-label">{t('catalog.structure.categoriesCount')}</p>
                  <p className="cat-summary-value">{categories.length}</p>
                </div>
              </>
            )}
            {selectedCategoryId && (
              <>
                <div className="cat-summary-divider" aria-hidden />
                <div>
                  <p className="subtle cat-summary-label">{t('catalog.structure.subcategoriesCount')}</p>
                  <p className="cat-summary-value">{subcategories.length}</p>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </>
  );
}
