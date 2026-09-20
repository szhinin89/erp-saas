/** Public assignment operations; delegates to the existing item endpoints. */
import { itemService } from "../api/itemService";

export const itemPriceListFacade = {
  getPriceLists: itemService.getPriceLists,
  setPriceLists: itemService.setPriceLists,
};
