export interface Product {
  id: string;
  saleCode: string;
  purchaseCode?: string;
  shortName: string;
  description: string;
  lineId: string;
  categoryId: string;
  subcategoryId: string;
  unitOfMeasureId: string;
  brandId: string;
  productTypeId: string;
  tariffId: string;
  saleTaxId: string;
  purchaseTaxId: string;
  exciseTaxId?: string;
  isService: boolean;
  isActive: boolean;
  availableOnWeb: boolean;
  availableOnMobile: boolean;
  isForSale: boolean;
  createdAt: string;
}
