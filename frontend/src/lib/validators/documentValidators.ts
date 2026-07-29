/** Módulo 10 — cédula ecuatoriana (10 dígitos). */
export function isValidCedula(num: string): boolean {
  if (!/^\d{10}$/.test(num)) return false;

  const province = parseInt(num.slice(0, 2), 10);
  if (province < 1 || province > 24) return false;

  const digits = num.split('').map(Number);
  const coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];

  const sum = coefficients.reduce((acc, coef, i) => {
    let val = coef * digits[i]!;
    if (val >= 10) val -= 9;
    return acc + val;
  }, 0);

  const verifier = (10 - (sum % 10)) % 10;

  return verifier === digits[9];
}
/**
 * Validador único de identificaciones Ecuador
 */
export function validateIdentification(
  identificationType: string,
  identificationNumber: string,
  personType?: number
): boolean {

  const value = identificationNumber.trim();

  switch (identificationType) {

    // RUC
    case '04':
      return isValidRuc(value, personType);


    // Cédula
    case '05':
      return isValidCedula(value);


    // Pasaporte
    case '06':
      return value.length > 0;


    // Consumidor final
    case '07':
      return value.length > 0;


    // Exterior
    case '08':
      return value.length > 0;


    // Placa
    case '09':
      return value.length > 0;


    default:
      return false;
  }
}


/**
 * RUC Ecuador
 */
function isValidRuc(
  num: string,
  personType?: number
): boolean {

  if (!/^\d{13}$/.test(num))
    return false;

  if (!num.endsWith('001'))
    return false;


  const thirdDigit = Number(num[2]);


  // Natural
  if (thirdDigit >= 0 && thirdDigit <= 5) {
    return isValidCedula(num.substring(0,10));
  }


  // Gobierno
  if (thirdDigit === 6) {
    return validatePublicRuc(num);
  }


  // Sociedad
  if (thirdDigit === 9) {
    return validatePrivateRuc(num);
  }


  return false;
}

function validatePrivateRuc(num:string):boolean {

  const digits = num.split('').map(Number);

  const coefficients = [
    4,3,2,7,6,5,4,3,2
  ];

  let sum = 0;

  for(let i=0;i<9;i++){
    sum += digits[i]! * coefficients[i]!;
  }

  let verifier = 11 - (sum % 11);

  if(verifier === 11)
    verifier = 0;

  if(verifier === 10)
    verifier = 1;

  return verifier === digits[9];
}


function validatePublicRuc(num:string):boolean {

  const digits = num.split('').map(Number);

  const coefficients = [
    3,2,7,6,5,4,3,2
  ];

  let sum = 0;

  for(let i=0;i<8;i++){
    sum += digits[i]! * coefficients[i]!;
  }

  let verifier = 11 - (sum % 11);

  if(verifier === 11)
    verifier = 0;

  if(verifier === 10)
    verifier = 1;

  return verifier === digits[8];
}

/** Módulo 10 — cédula ecuatoriana (10 dígitos). 
export function isValidCedula(num: string): boolean {
  if (!/^\d{10}$/.test(num)) return false;
  const province = parseInt(num.slice(0, 2), 10);
  if (province < 1 || province > 24) return false;
  const digits = num.split('').map(Number);
  const coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];
  const sum = coefficients.reduce((acc, coef, i) => {
    let val = coef * digits[i]!;
    if (val >= 10) val -= 9;
    return acc + val;
  }, 0);
  const verifier = (10 - (sum % 10)) % 10;
  return verifier === digits[9];
}

/** RUC Ecuador (13 dígitos). Los primeros 10 deben ser cédula válida; últimos 3 = "001". 
export function isValidRuc(num: string): boolean {
  if (!/^\d{13}$/.test(num)) return false;
  if (!num.endsWith('001')) return false;
  return isValidCedula(num.slice(0, 10));
}
**/