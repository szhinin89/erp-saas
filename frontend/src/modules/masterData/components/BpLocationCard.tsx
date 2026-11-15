import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import type { BpLocationDto } from "../types/businessPartner.types";

/**
 * Tarjeta de una ubicación de la ficha de Business Partner. Presentacional: los permisos
 * y las acciones (editar, principal, activar/desactivar) las decide BpLocationsSection.
 */
export function BpLocationCard({
  location: l,
  canUpdate,
  canDisable,
  saving,
  onEdit,
  onSetPrimary,
  onToggle,
}: {
  location: BpLocationDto;
  canUpdate: boolean;
  canDisable: boolean;
  saving: boolean;
  onEdit: (location: BpLocationDto) => void;
  onSetPrimary: (location: BpLocationDto) => void;
  onToggle: (location: BpLocationDto) => void;
}) {
  return (
    <div
      className={`md-location-card ${!l.isActive ? "md-location-card--inactive" : ""}`}
    >
      <div className="md-location-card__header">
        <span className="md-location-card__name zh-row-title">
          {l.name}
        </span>
        {l.isPrimary && <Badge variant="success" label="Principal" />}
        <Badge variant="neutral" label={l.typeLabel} />
      </div>
      <p className="md-location-card__address">{l.addressLine}</p>
      {l.purposes.length > 0 && (
        <div className="md-location-card__purposes">
          {l.purposes.map((p) => (
            <Badge key={p} variant="neutral" label={p} />
          ))}
        </div>
      )}
      {(l.phone || l.email) && (
        <p className="md-location-card__contact">
          {l.phone && <span>📞 {l.phone}</span>}
          {l.email && <span>✉ {l.email}</span>}
        </p>
      )}
      {(canUpdate || canDisable) && (
        <div className="md-location-card__actions">
          {canUpdate && l.isActive && (
            <ZHBtn
              variant="ghost"
              size="sm"
              onClick={() => onEdit(l)}
            >
              Editar
            </ZHBtn>
          )}
          {canUpdate && l.isActive && !l.isPrimary && (
            <ZHBtn
              variant="ghost"
              size="sm"
              disabled={saving}
              onClick={() => onSetPrimary(l)}
            >
              Principal
            </ZHBtn>
          )}
          {(l.isActive ? canDisable : canUpdate) && (
            <ZHBtn
              variant="ghost"
              size="sm"
              disabled={saving}
              onClick={() => onToggle(l)}
            >
              {l.isActive ? "Desactivar" : "Activar"}
            </ZHBtn>
          )}
        </div>
      )}
    </div>
  );
}
