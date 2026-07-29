import type { NavItem, TranslateFn } from "../../../../nav/navConfig";

type FavoriteButtonProps = {
  item: NavItem;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
};

/** Estrella para marcar/desmarcar un ítem de navegación como favorito (identidad por NavItem.id). */
export function FavoriteButton({
  item,
  isFavorite,
  toggleFavorite,
  t,
}: FavoriteButtonProps) {
  if (!item.to) return null;
  const on = isFavorite(item.id);

  return (
    <button
      type="button"
      className={`zh-launcher__fav${on ? " is-on" : ""}`}
      aria-label={`${on ? t("app.favorites.remove") : t("app.favorites.add")}: ${item.label}`}
      aria-pressed={on}
      onClick={(e) => {
        e.preventDefault();
        e.stopPropagation();
        toggleFavorite(item);
      }}
    >
      <span
        className="material-symbols-outlined zh-launcher__favIcon"
        aria-hidden="true"
      >
        star
      </span>
    </button>
  );
}
