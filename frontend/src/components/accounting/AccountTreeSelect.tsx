import { useMemo, useState } from 'react';
import type { Account } from '../../types/accounting';
import { ZHBtn } from '../zh/ZHForm';
import './AccountTreeSelect.css';

type FlatNode = {
  id: string;
  label: string;
  depth: number;
  disabled: boolean;
};

function buildTreeOptions(accounts: Account[]): FlatNode[] {
  const byParent = new Map<string | null, Account[]>();
  for (const a of accounts) {
    const pid = a.parentId ?? null;
    const arr = byParent.get(pid) ?? [];
    arr.push(a);
    byParent.set(pid, arr);
  }
  for (const arr of byParent.values()) {
    arr.sort((a, b) => a.code.localeCompare(b.code));
  }

  const out: FlatNode[] = [];
  const walk = (parentId: string | null, depth: number, visited: Set<string>) => {
    const children = byParent.get(parentId) ?? [];
    for (const a of children) {
      if (visited.has(a.id)) continue;
      visited.add(a.id);
      out.push({
        id: a.id,
        label: `${a.code} — ${a.name}`,
        depth,
        disabled: a.isActive === false || a.allowsMovements === false,
      });
      walk(a.id, depth + 1, visited);
    }
  };

  walk(null, 0, new Set<string>());
  return out;
}

export function AccountTreeSelect(props: {
  value: string | null;
  onChange: (next: string | null) => void;
  accounts: Account[];
  disabled?: boolean;
  placeholder?: string;
}) {
  const { value, onChange, accounts, disabled, placeholder } = props;
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');

  const flat = useMemo(() => buildTreeOptions(accounts), [accounts]);
  const selected = useMemo(() => flat.find((x) => x.id === value) ?? null, [flat, value]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return flat;
    return flat.filter((x) => x.label.toLowerCase().includes(query));
  }, [flat, q]);

  return (
    <div className="ats">
      <button
        type="button"
        className="ats__control"
        disabled={disabled}
        onClick={() => setOpen((s) => !s)}
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className="ats__value">
          {selected?.label ?? (placeholder ?? 'Seleccionar')}
        </span>
        <span className="ats__chev" aria-hidden />
      </button>

      {open ? (
        <div className="ats__popup" role="dialog">
          <div className="ats__searchRow">
            <input
              className="ats__search"
              type="search"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Buscar cuenta…"
              autoComplete="off"
              spellCheck={false}
            />
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => {
                onChange(null);
                setOpen(false);
              }}
              disabled={disabled}
            >
              Limpiar
            </ZHBtn>
          </div>

          <div className="ats__list" role="listbox">
            {filtered.length === 0 ? (
              <div className="ats__empty">Sin resultados</div>
            ) : (
              filtered.map((x) => (
                <button
                  key={x.id}
                  type="button"
                  role="option"
                  aria-selected={x.id === value}
                  className={`ats__opt${x.id === value ? ' is-selected' : ''}`}
                  disabled={disabled || x.disabled}
                  onClick={() => {
                    onChange(x.id);
                    setOpen(false);
                  }}
                >
                  <span className="ats__indent" style={{ width: `${x.depth * 14}px` }} aria-hidden />
                  <span className="ats__optLabel">{x.label}</span>
                  {x.disabled ? <span className="ats__optMeta">No usable</span> : null}
                </button>
              ))
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

