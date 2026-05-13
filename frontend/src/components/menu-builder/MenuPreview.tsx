import { useState } from 'react';
import { useI18n } from '../../i18n/i18n';
import type { MenuItem } from './menuBuilderTypes';

export type MenuPreviewLayout = 'vertical' | 'horizontal';

type Props = {
  items: MenuItem[];
  layout: MenuPreviewLayout;
  className?: string;
};

function isPreviewFolder(item: MenuItem): boolean {
  return !(item.ruta?.trim()) && !(item.permiso?.trim());
}

function MenuItemPreview({ item, layout }: { item: MenuItem; layout: MenuPreviewLayout }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const children = item.children ?? [];
  const hasChildren = children.length > 0;
  const folder = isPreviewFolder(item);
  const leaf = !folder;

  if (layout === 'horizontal') {
    return (
      <div className="menu-preview-item">
        {folder && hasChildren ? (
          <>
            <button
              type="button"
              className="menu-preview-item__btn"
              onClick={() => setOpen((o) => !o)}
              aria-expanded={open}
            >
              {item.icono ? <span className="menu-preview-item__icon">{item.icono}</span> : null}
              <span className="menu-preview-item__label">{item.nombre}</span>
              <span className="menu-preview-item__chev">{open ? '▾' : '▸'}</span>
            </button>
            {open ? (
              <div className="menu-preview-dropdown">
                {children.map((c) => (
                  <div key={c.id} className="menu-preview-dropdown__nest">
                    <MenuItemPreview item={c} layout="horizontal" />
                  </div>
                ))}
              </div>
            ) : null}
          </>
        ) : leaf ? (
          <div className="menu-preview-item__leafRow">
            {item.icono ? <span className="menu-preview-item__icon">{item.icono}</span> : null}
            <span className="menu-preview-item__linkLabel">{item.nombre}</span>
            {item.ruta ? (
              <span className="menu-preview-item__routeHint" title={item.ruta}>
                {item.ruta}
              </span>
            ) : null}
          </div>
        ) : (
          <div className="menu-preview-item__muted">{item.nombre}</div>
        )}
      </div>
    );
  }

  return (
    <div className="menu-preview-item menu-preview-item--block">
      {folder && hasChildren ? (
        <>
          <button
            type="button"
            className="menu-preview-item__btn"
            onClick={() => setOpen((o) => !o)}
            aria-expanded={open}
          >
            {item.icono ? (
              <span className="menu-preview-item__icon">{item.icono}</span>
            ) : (
              <span className="menu-preview-item__icon menu-preview-item__icon--spacer" aria-hidden />
            )}
            <span className="menu-preview-item__label">{item.nombre}</span>
            <span className="menu-preview-item__chev">{open ? '▾' : '▸'}</span>
          </button>
          {open ? (
            <div className="menu-preview-children">
              {children.map((c) => (
                <MenuItemPreview key={c.id} item={c} layout={layout} />
              ))}
            </div>
          ) : null}
        </>
      ) : leaf ? (
        <div className="menu-preview-item__leafBlock">
          {item.icono ? (
            <span className="menu-preview-item__icon">{item.icono}</span>
          ) : (
            <span className="menu-preview-item__icon menu-preview-item__icon--spacer" aria-hidden />
          )}
          <div className="menu-preview-item__leafText">
            <span className="menu-preview-item__linkLabel">{item.nombre}</span>
            {item.ruta ? (
              <span className="menu-preview-item__routeHint" title={item.ruta}>
                {item.ruta}
              </span>
            ) : null}
          </div>
        </div>
      ) : (
        <div className="menu-preview-item__muted" title={t('superadmin.menuBuilder.previewEmptyFolder')}>
          {item.nombre}
        </div>
      )}
    </div>
  );
}

export function MenuPreview({ items, layout, className = '' }: Props) {
  const { t } = useI18n();

  const empty = (
    <div className="menu-preview-empty">
      <span className="menu-preview-empty__icon" aria-hidden>
        ◇
      </span>
      {t('superadmin.menuBuilder.previewEmpty')}
    </div>
  );

  if (layout === 'horizontal') {
    return (
      <nav className={`menu-preview-nav menu-preview-nav--horizontal ${className}`.trim()} aria-label="Menu preview">
        {items.length === 0 ? (
          empty
        ) : (
          <div className="menu-preview-nav__inner">
            {items.map((item) => (
              <MenuItemPreview key={item.id} item={item} layout="horizontal" />
            ))}
          </div>
        )}
      </nav>
    );
  }

  return (
    <nav className={`menu-preview-nav menu-preview-nav--vertical ${className}`.trim()} aria-label="Menu preview">
      {items.length === 0 ? (
        empty
      ) : (
        items.map((item) => <MenuItemPreview key={item.id} item={item} layout="vertical" />)
      )}
    </nav>
  );
}
