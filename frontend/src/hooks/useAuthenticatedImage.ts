import { useEffect, useState } from "react";
import { api } from "../modules/lib/api";

/**
 * Descarga `url` con el cliente autenticado (Bearer token) y expone un object URL
 * apto para `<img src>`. Necesario porque rutas de contenido como
 * `/api/v1/companies/profile/logo/content` requieren Authorization header,
 * que un `<img>` no puede enviar directamente.
 */
export function useAuthenticatedImage(
  url: string | null | undefined,
): string | null {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!url) {
      setObjectUrl(null);
      return;
    }

    let cancelled = false;
    let created: string | null = null;

    api
      .get<Blob>(url, { responseType: "blob" })
      .then(({ data }) => {
        if (cancelled) return;
        created = URL.createObjectURL(data);
        setObjectUrl(created);
      })
      .catch(() => {
        if (!cancelled) setObjectUrl(null);
      });

    return () => {
      cancelled = true;
      if (created) URL.revokeObjectURL(created);
    };
  }, [url]);

  return objectUrl;
}
