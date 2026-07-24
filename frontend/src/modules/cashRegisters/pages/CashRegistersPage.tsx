import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { CashRegistersManagementSection } from '../components/CashRegistersManagementSection';

export function CashRegistersPage() {
  return (
    <ErpPageTemplate kicker="Caja" title="Administración de Cajas">
      <CashRegistersManagementSection />
    </ErpPageTemplate>
  );
}
