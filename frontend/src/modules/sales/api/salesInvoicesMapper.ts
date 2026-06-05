import type { SalesInvoiceDetailDto, SalesInvoiceDto, SalesInvoiceLineDto } from './salesInvoicesService';

interface ApiSalesBillDto {
  id: string;
  businessPartnerId: string;
  customerName: string;
  warehouseId: string;
  branchId: string;
  estabCode: string;
  emPointCode: string;
  sequential: string;
  accessKey: string;
  issueDate: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  status: string;
  authNumber?: string | null;
  authDate?: string | null;
  errorMessage?: string | null;
  journalEntryId?: string | null;
  createdAt: string;
}

interface ApiSalesBillDetailDto extends ApiSalesBillDto {
  docType: string;
  xmlSignedPath?: string | null;
  xmlAuthPath?: string | null;
  lines: ApiSalesDetailDto[];
}

interface ApiSalesDetailDto {
  id: string;
  productId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  vatTotal: number;
  total: number;
}

interface ApiSalesPagedResult {
  items: ApiSalesBillDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export function mapSalesBillToVentasFactura(raw: ApiSalesBillDto): SalesInvoiceDto {
  return {
    id: raw.id,
    clienteId: raw.businessPartnerId,
    clienteNombre: raw.customerName,
    businessPartnerId: raw.businessPartnerId,
    warehouseId: raw.warehouseId,
    sucursalId: raw.branchId,
    establecimiento: raw.estabCode,
    puntoEmision: raw.emPointCode,
    secuencial: raw.sequential,
    claveAcceso: raw.accessKey,
    issueDate: raw.issueDate,
    subtotal: raw.subtotal,
    vatTotal: raw.vatTotal,
    total: raw.total,
    status: raw.status,
    numeroAutorizacion: raw.authNumber ?? null,
    fechaAutorizacion: raw.authDate ?? null,
    mensajeError: raw.errorMessage ?? null,
    asientoContableId: raw.journalEntryId ?? null,
    createdAt: raw.createdAt,
  };
}

function mapLine(raw: ApiSalesDetailDto): SalesInvoiceLineDto {
  return {
    id: raw.id,
    productId: raw.productId,
    description: raw.description,
    quantity: raw.quantity,
    unitPrice: raw.unitPrice,
    subtotal: raw.subtotal,
    vatTotal: raw.vatTotal,
    total: raw.total,
  };
}

export function mapSalesBillDetailToVentasFactura(raw: ApiSalesBillDetailDto): SalesInvoiceDetailDto {
  const header = mapSalesBillToVentasFactura(raw);
  return {
    ...header,
    docType: raw.docType,
    xmlSignedPath: raw.xmlSignedPath ?? null,
    xmlAuthPath: raw.xmlAuthPath ?? null,
    lines: (raw.lines ?? []).map(mapLine),
  };
}

export function mapSalesPagedResult(raw: ApiSalesPagedResult) {
  return {
    items: (raw.items ?? []).map(mapSalesBillToVentasFactura),
    totalCount: raw.totalCount,
    pageNumber: raw.pageNumber,
    pageSize: raw.pageSize,
  };
}

export function invoiceNumber(row: Pick<SalesInvoiceDto, 'establecimiento' | 'puntoEmision' | 'secuencial'>): string {
  return `${row.establecimiento}-${row.puntoEmision}-${row.secuencial}`;
}
