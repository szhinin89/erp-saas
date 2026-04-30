import { useState } from 'react';
import { productService } from '../services/productService';
import { useAsync } from '../hooks/useAsync';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge,
} from '../components/PageShell';
import { CreateProductModal } from '../components/CreateProductModal';

export function ProductsPage() {
  const { data: products, loading, error, refetch } = useAsync(productService.getAll);
  const [showModal, setShowModal] = useState(false);

  return (
    <PageShell
      title="Productos"
      action={<button type="button" onClick={() => setShowModal(true)}>+ Nuevo producto</button>}
    >
      {showModal && (
        <CreateProductModal
          onClose={() => setShowModal(false)}
          onCreated={refetch}
        />
      )}
      {loading && <LoadingState />}
      {error   && <ErrorState message={error} />}
      {!loading && !error && products?.length === 0 && (
        <EmptyState message="No hay productos registrados aún." />
      )}
      {!loading && !error && products && products.length > 0 && (
        <TableCard>
          <table>
            <thead>
              <tr>
                <th>Código</th>
                <th>Nombre</th>
                <th>Descripción</th>
                <th>Tipo</th>
                <th>Venta</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id}>
                  <td><span className="mono">{p.saleCode}</span></td>
                  <td>{p.shortName}</td>
                  <td style={{ color: '#6b7280' }}>{p.description}</td>
                  <td>
                    <Badge
                      label={p.isService ? 'Servicio' : 'Producto'}
                      variant={p.isService ? 'blue' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isForSale ? 'Sí' : 'No'}
                      variant={p.isForSale ? 'green' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isActive ? 'Activo' : 'Inactivo'}
                      variant={p.isActive ? 'green' : 'red'}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableCard>
      )}
    </PageShell>
  );
}
