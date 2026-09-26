import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from "react";
import { ZHBtn } from "../ZHForm";
import { ZHIconButton } from "../ZHIconButton";
import { ZHPickerResultItem } from "../ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../ZHPickerSelectedValue";
import { ZhTextInput } from "./ZhTextInput";
import { buildSearchIndex, filterSearchIndex } from "./searchSelectFilter";

const DEFAULT_MAX_RESULTS = 30;
const DEFAULT_REMOTE_DEBOUNCE_MS = 300;
const DEFAULT_REMOTE_MIN_QUERY = 2;

interface ZhSearchSelectCommonProps<T> {
  /** Clave estable y única de cada opción. */
  getOptionKey: (option: T) => string;
  /** Texto principal del resultado y de la chip/tarjeta seleccionada. */
  getOptionLabel: (option: T) => string;
  /** Línea secundaria del resultado (p.ej. identificación). */
  getOptionDescription?: (option: T) => ReactNode;
  getOptionMeta?: (option: T) => ReactNode;
  /** Texto de la chip en modo múltiple. Default: `getOptionLabel`. */
  getChipLabel?: (option: T) => string;

  /** Datasource LOCAL: se filtra en memoria conforme se escribe. Pasar una referencia estable
   * (memoizada) — el índice normalizado se recalcula solo cuando cambia. */
  options?: readonly T[];
  /** Texto buscable de cada opción en modo local. Default: `getOptionLabel`. Debe ser estable. */
  getOptionSearchText?: (option: T) => string;

  /** Datasource REMOTO: se invoca con debounce; la búsqueda anterior se cancela vía `signal`.
   * Debe ser estable (useCallback). Si se pasan `options` y `loadOptions`, gana `loadOptions`. */
  loadOptions?: (query: string, signal: AbortSignal) => Promise<readonly T[]>;
  /** Default: 300 ms remoto, 0 local. */
  debounceMs?: number;
  /** Caracteres mínimos para buscar. Default: 2 remoto, 0 local (al enfocar muestra los primeros). */
  minQueryLength?: number;
  /** Máximo de resultados visibles. Default 30. */
  maxResults?: number;

  placeholder?: string;
  /** Estado vacío: texto fijo o contenido derivado de la consulta (p.ej. "Sin resultados para X"
   * + enlace de alta). */
  emptyText?: ReactNode | ((query: string) => ReactNode);
  loadingText?: string;
  /** Se muestra al pie cuando hay más coincidencias que `maxResults`. */
  truncatedText?: string;
  clearLabel?: string;
  removeLabel?: string;
  disabled?: boolean;
  id?: string;
  "aria-label"?: string;
  className?: string;
}

export interface ZhSearchSelectSingleProps<T> extends ZhSearchSelectCommonProps<T> {
  mode?: "single";
  value: T | null;
  onChange: (value: T | null) => void;
}

export interface ZhSearchSelectMultipleProps<T> extends ZhSearchSelectCommonProps<T> {
  mode: "multiple";
  value: readonly T[];
  onChange: (value: T[]) => void;
}

export type ZhSearchSelectProps<T> =
  | ZhSearchSelectSingleProps<T>
  | ZhSearchSelectMultipleProps<T>;

/**
 * ZH-SUPPLIER-SEARCH-REUSABLE-01 — autocomplete genérico del Design System (single/multiple) con
 * datasource local o remoto. No conoce ningún dominio: los wrappers (p.ej. `SupplierSearchSelect`)
 * deciden qué buscar y cómo mostrarlo. Nunca renderiza más de `maxResults` opciones, así sirve
 * para catálogos de miles de registros sin un `<select>` gigante.
 *
 * Reutiliza el chrome existente de pickers: `zh-picker*`, `ZHPickerResultItem`,
 * `ZHPickerSelectedValue` (single) y chips `ZHBtn` removibles (multiple).
 */
export function ZhSearchSelect<T>(props: ZhSearchSelectProps<T>) {
  const {
    getOptionKey,
    getOptionLabel,
    getOptionDescription,
    getOptionMeta,
    getChipLabel,
    options,
    getOptionSearchText,
    loadOptions,
    maxResults = DEFAULT_MAX_RESULTS,
    placeholder = "Buscar...",
    emptyText = "Sin resultados",
    loadingText = "Buscando...",
    truncatedText = "Siga escribiendo para refinar la búsqueda.",
    clearLabel = "Limpiar selección",
    removeLabel = "Quitar",
    disabled = false,
    id,
    className,
  } = props;
  const isRemote = loadOptions !== undefined;
  const debounceMs = props.debounceMs ?? (isRemote ? DEFAULT_REMOTE_DEBOUNCE_MS : 0);
  const minQueryLength =
    props.minQueryLength ?? (isRemote ? DEFAULT_REMOTE_MIN_QUERY : 0);

  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [activeIdx, setActiveIdx] = useState(-1);
  const [remoteResults, setRemoteResults] = useState<readonly T[]>([]);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const autoId = useId();
  const inputId = id ?? `zh-search-select-${autoId}`;
  const listId = `${inputId}-listbox`;

  const { mode, value: rawValue } = props;
  const selected = useMemo<readonly T[]>(() => {
    if (mode === "multiple") return rawValue as readonly T[];
    return rawValue ? [rawValue as T] : [];
  }, [mode, rawValue]);
  const selectedKeys = useMemo(
    () => new Set(selected.map(getOptionKey)),
    [selected, getOptionKey],
  );

  const trimmedQuery = query.trim();
  const queryReady = trimmedQuery.length >= minQueryLength;

  // ── Local ──────────────────────────────────────────────────────────────────
  const searchText = getOptionSearchText ?? getOptionLabel;
  const localIndex = useMemo(
    () => (isRemote ? [] : buildSearchIndex(options ?? [], getOptionKey, searchText)),
    [isRemote, options, getOptionKey, searchText],
  );
  // Se pide uno extra para saber si hubo truncamiento sin recorrer todo el catálogo.
  const localMatches = useMemo(
    () =>
      isRemote || !queryReady
        ? []
        : filterSearchIndex(localIndex, query, maxResults + 1, selectedKeys),
    [isRemote, queryReady, localIndex, query, maxResults, selectedKeys],
  );

  // ── Remoto: debounce + cancelación de la búsqueda anterior ─────────────────
  useEffect(() => {
    if (!loadOptions || !open || trimmedQuery.length < minQueryLength) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setLoading(true);
      loadOptions(trimmedQuery, controller.signal)
        .then((rows) => {
          if (!controller.signal.aborted) setRemoteResults(rows);
        })
        .catch(() => {
          if (!controller.signal.aborted) setRemoteResults([]);
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
    }, debounceMs);
    return () => {
      clearTimeout(timer);
      controller.abort();
      setLoading(false);
    };
  }, [loadOptions, open, trimmedQuery, minQueryLength, debounceMs]);

  const remoteMatches = useMemo(
    () =>
      !isRemote || !queryReady
        ? []
        : remoteResults.filter((o) => !selectedKeys.has(getOptionKey(o))),
    [isRemote, queryReady, remoteResults, selectedKeys, getOptionKey],
  );

  const matches = isRemote ? remoteMatches : localMatches;
  const truncated = matches.length > maxResults;
  const results = truncated ? matches.slice(0, maxResults) : matches;
  const showDropdown = open && !disabled && queryReady;
  const emptyContent =
    typeof emptyText === "function" ? emptyText(trimmedQuery) : emptyText;

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const select = (option: T) => {
    setQuery("");
    setActiveIdx(-1);
    if (props.mode === "multiple") {
      props.onChange([...props.value, option]);
      inputRef.current?.focus();
    } else {
      props.onChange(option);
      setOpen(false);
    }
  };

  const remove = (key: string) => {
    if (props.mode === "multiple")
      props.onChange(props.value.filter((o) => getOptionKey(o) !== key));
  };

  const clearAll = () => {
    setQuery("");
    setActiveIdx(-1);
    if (props.mode === "multiple") props.onChange([]);
    else props.onChange(null);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setOpen(true);
      setActiveIdx((i) => Math.min(i + 1, results.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIdx((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      if (!showDropdown || results.length === 0) return;
      e.preventDefault();
      select(results[Math.max(activeIdx, 0)]);
    } else if (e.key === "Escape") {
      if (open) setOpen(false);
      else setQuery("");
    } else if (
      e.key === "Backspace" &&
      query === "" &&
      props.mode === "multiple" &&
      props.value.length > 0
    ) {
      remove(getOptionKey(props.value.at(-1) as T));
    }
  };

  const rootCls = ["zh-picker", "zh-search-select", className].filter(Boolean).join(" ");

  if (props.mode !== "multiple" && props.value) {
    const value = props.value;
    return (
      <div className={rootCls}>
        <ZHPickerSelectedValue
          title={getOptionLabel(value)}
          subtitle={getOptionDescription?.(value)}
          meta={getOptionMeta?.(value)}
          clearLabel={clearLabel}
          onClear={disabled ? undefined : clearAll}
        />
      </div>
    );
  }

  const hasClearable = query !== "" || selected.length > 0;
  const activeOptionId =
    showDropdown && activeIdx >= 0 && activeIdx < results.length
      ? `${listId}-opt-${activeIdx}`
      : undefined;

  return (
    <div ref={wrapRef} className={rootCls}>
      {props.mode === "multiple" && props.value.length > 0 && (
        <div className="zh-search-select__chips">
          {props.value.map((option) => {
            const key = getOptionKey(option);
            const label = (getChipLabel ?? getOptionLabel)(option);
            return (
              <ZHBtn
                key={key}
                variant="secondary"
                size="xs"
                type="button"
                disabled={disabled}
                aria-label={`${removeLabel} ${label}`}
                onClick={() => remove(key)}
              >
                {label}
                <span className="material-symbols-outlined zh-icon-sm">close</span>
              </ZHBtn>
            );
          })}
        </div>
      )}
      <div className="zh-picker__input-wrap">
        <ZhTextInput
          ref={inputRef}
          id={inputId}
          className="zh-search-select__input"
          role="combobox"
          aria-label={props["aria-label"]}
          aria-autocomplete="list"
          aria-expanded={showDropdown}
          aria-controls={listId}
          aria-activedescendant={activeOptionId}
          autoComplete="off"
          value={query}
          disabled={disabled}
          placeholder={placeholder}
          onChange={(e) => {
            setQuery(e.target.value);
            setActiveIdx(-1);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleKeyDown}
        />
        <div className="zh-search-select__trailing">
          {loading && <span className="zh-search-select__loading">{loadingText}</span>}
          {hasClearable && !disabled && (
            <ZHIconButton icon="close" title={clearLabel} variant="ghost" onClick={clearAll} />
          )}
        </div>
      </div>

      {showDropdown && (
        <div id={listId} role="listbox" className="zh-picker__dropdown">
          {results.length === 0 && (
            <div className="zh-picker__empty">
              {loading ? loadingText : emptyContent}
            </div>
          )}
          {results.map((option, i) => (
            <ZHPickerResultItem
              key={getOptionKey(option)}
              id={`${listId}-opt-${i}`}
              role="option"
              tabIndex={-1}
              title={getOptionLabel(option)}
              subtitle={getOptionDescription?.(option)}
              meta={getOptionMeta?.(option)}
              selected={i === activeIdx}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => select(option)}
              onMouseEnter={() => setActiveIdx(i)}
            />
          ))}
          {truncated && <div className="zh-picker__empty-message zh-search-select__hint">{truncatedText}</div>}
        </div>
      )}
    </div>
  );
}
