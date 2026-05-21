import { Fragment, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { NavItem } from '../nav/navConfig';

/** True si la ruta actual coincide con este ítem o con algún descendiente (menú anidado). */
export function navSubtreeMatchesPath(it: NavItem, pathname: string): boolean {
  if (it.to) {
    const ok = pathname === it.to || (it.to.length > 1 && pathname.startsWith(`${it.to}/`));
    if (ok) return true;
  }
  return it.children?.some((c) => navSubtreeMatchesPath(c, pathname)) ?? false;
}

/** Ramas con hijos (p. ej. Administración de ítems): cerradas al abrir el menú; clic en la fila despliega Nuevo, Marca, … */
function MainMenuBranchRow({
  it,
  depth,
  onClose,
  isFavorite,
  toggleFavorite,
  t,
  showFavoriteStars,
}: {
  it: NavItem;
  depth: number;
  onClose: () => void;
  isFavorite: (to: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: (key: string) => string;
  showFavoriteStars: boolean;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="app-mainmenu-branchRoot">
      <div className={`app-mainmenu-row${depth > 0 ? ' is-nested' : ''}`}>
        <button
          type="button"
          className="app-mainmenu-link app-mainmenu-navBtn app-mainmenu-branchToggle"
          aria-expanded={expanded}
          onClick={() => setExpanded((v) => !v)}
        >
          {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
          <span className="app-mainmenu-branchLabel">{it.label}</span>
          <span className="app-mainmenu-branchCaret" aria-hidden>
            {expanded ? '▾' : '▸'}
          </span>
        </button>
        {it.to && showFavoriteStars ? (
          <button
            type="button"
            className={`app-mainmenu-fav${isFavorite(it.to) ? ' is-on' : ''}`}
            aria-label={isFavorite(it.to) ? t('app.favorites.remove') : t('app.favorites.add')}
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              toggleFavorite(it);
            }}
          >
            {isFavorite(it.to) ? '★' : '☆'}
          </button>
        ) : null}
      </div>
      {expanded ? (
        <div className="app-mainmenu-branchChildren">
          <MainMenuList
            items={it.children!}
            depth={depth + 1}
            onClose={onClose}
            isFavorite={isFavorite}
            toggleFavorite={toggleFavorite}
            t={t}
            showFavoriteStars={showFavoriteStars}
          />
        </div>
      ) : null}
    </div>
  );
}

export function MainMenuList({
  items,
  depth,
  onClose,
  isFavorite,
  toggleFavorite,
  t,
  showFavoriteStars,
}: {
  items: NavItem[];
  depth: number;
  onClose: () => void;
  isFavorite: (to: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: (key: string) => string;
  showFavoriteStars: boolean;
}) {
  const navigate = useNavigate();

  return (
    <>
      {items.map((it, idx) =>
        it.children?.length ? (
          <MainMenuBranchRow
            key={`${depth}-b-${idx}-${it.label}`}
            it={it}
            depth={depth}
            onClose={onClose}
            isFavorite={isFavorite}
            toggleFavorite={toggleFavorite}
            t={t}
            showFavoriteStars={showFavoriteStars}
          />
        ) : (
          <Fragment key={`${depth}-${it.label}-${it.to}-${idx}`}>
            <div className={`app-mainmenu-row${depth > 0 ? ' is-nested' : ''}`}>
              {it.to ? (
                <button
                  type="button"
                  className="app-mainmenu-link app-mainmenu-navBtn"
                  onClick={() => {
                    navigate(it.to);
                    onClose();
                  }}
                >
                  {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
                  <span>{it.label}</span>
                </button>
              ) : (
                <span className="app-mainmenu-link app-mainmenu-parent" title={t('app.layout.menuMissingRoute')}>
                  {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
                  <span>{it.label}</span>
                </span>
              )}
              {it.to && showFavoriteStars ? (
                <button
                  type="button"
                  className={`app-mainmenu-fav${isFavorite(it.to) ? ' is-on' : ''}`}
                  aria-label={isFavorite(it.to) ? t('app.favorites.remove') : t('app.favorites.add')}
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    toggleFavorite(it);
                  }}
                >
                  {isFavorite(it.to) ? '★' : '☆'}
                </button>
              ) : null}
            </div>
          </Fragment>
        ),
      )}
    </>
  );
}
