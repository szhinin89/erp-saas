-- ============================================================
-- CATÁLOGOS FIJOS SRI - COMPROBANTES ELECTRÓNICOS
-- Basado en Ficha Técnica Versión 2.32 (Octubre 2025)
-- Aplicable a: Ecuador - Sistema de Facturación Electrónica
-- ============================================================
-- Estas tablas contienen valores definidos por el SRI.
-- Son IGUALES para todos los contribuyentes.
-- NO deben ser modificadas por el usuario final.
-- ============================================================
-- =========================================================
-- SRI CATALOGS - COMMON (no company dependency)
-- Basado en Ficha Técnica SRI v2.32 (Octubre 2025)
-- Ejecutar este script UNA SOLA VEZ en la base de datos.
-- =========================================================
-- =========================================================
-- 1) SRI IMMUTABLE CATALOGS (prefix: sri_)
-- =========================================================

CREATE TABLE IF NOT EXISTS sri_emission_type (
    emission_code     VARCHAR(1) PRIMARY KEY,
    emission_name     VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_document_type (
    document_code     VARCHAR(2) PRIMARY KEY,
    document_name     VARCHAR(120) NOT NULL,
    xml_tag           VARCHAR(40)
);

CREATE TABLE IF NOT EXISTS sri_environment_type (
    env_code          VARCHAR(1) PRIMARY KEY,
    env_name          VARCHAR(40) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_identification_type (
    ident_code        VARCHAR(2) PRIMARY KEY,
    ident_name        VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_tax_code (
    tax_code          VARCHAR(2) PRIMARY KEY,
    tax_name          VARCHAR(60) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_vat_rate (
    vat_code          VARCHAR(2) PRIMARY KEY,
    vat_name          VARCHAR(80) NOT NULL,
    rate_pct          NUMERIC(7,4)
);

CREATE TABLE IF NOT EXISTS sri_ice_rate (
    ice_code          VARCHAR(4) PRIMARY KEY,
    ice_name          VARCHAR(180) NOT NULL,
    ad_val_pct        NUMERIC(7,4),
    spec_rate         NUMERIC(10,4),
    rate_type         VARCHAR(20) DEFAULT 'AD_VALOREM',
    rate_note         VARCHAR(120),
    valid_from        DATE,
    valid_to          DATE,
    is_current        BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS sri_withholding_tax_type (
    wh_tax_code       VARCHAR(2) PRIMARY KEY,
    wh_tax_name       VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_vat_withholding_rate (
    vat_wh_code       VARCHAR(4) PRIMARY KEY,
    rate_pct          NUMERIC(7,4) NOT NULL,
    rate_name         VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_isd_withholding_rate (
    isd_wh_code       VARCHAR(8) NOT NULL,
    rate_pct          NUMERIC(7,4) NOT NULL,
    valid_from        DATE NOT NULL,
    valid_to          DATE,
    is_current        BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (isd_wh_code, valid_from)
);

CREATE TABLE IF NOT EXISTS sri_presumptive_vat_withholding_rate (
    pres_vat_code     VARCHAR(4) PRIMARY KEY,
    rate_pct          NUMERIC(7,4) NOT NULL,
    rate_name         VARCHAR(120) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_oil_derivatives_income_withholding (
    oil_inc_code      VARCHAR(4) PRIMARY KEY,
    rate_pct          NUMERIC(9,6) NOT NULL,
    rate_name         VARCHAR(120) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_payment_method (
    payment_code      VARCHAR(2) PRIMARY KEY,
    payment_name      VARCHAR(120) NOT NULL,
    start_date        DATE,
    end_date          DATE
);

CREATE TABLE IF NOT EXISTS sri_document_status (
    status_code       VARCHAR(3) PRIMARY KEY,
    status_name       VARCHAR(40) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_country (
    country_code      VARCHAR(3) PRIMARY KEY,
    country_name      VARCHAR(120) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_supporting_document (
    support_code      VARCHAR(2) PRIMARY KEY,
    support_name      VARCHAR(140) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_reimbursement_provider_type (
    provider_code     VARCHAR(2) PRIMARY KEY,
    provider_name     VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_error_code (
    error_code        VARCHAR(10) PRIMARY KEY,
    error_type        VARCHAR(20) NOT NULL, -- ERROR / WARNING / DIG
    error_name        VARCHAR(140) NOT NULL,
    error_desc        TEXT,
    source_area       VARCHAR(25)
);

CREATE TABLE IF NOT EXISTS sri_regime_type (
    regime_code       VARCHAR(4) PRIMARY KEY,
    regime_name       VARCHAR(120) NOT NULL
);

CREATE TABLE IF NOT EXISTS sri_tax_support_code (
    support_tax_code  VARCHAR(4) PRIMARY KEY,
    support_tax_name  VARCHAR(180) NOT NULL
);

-- =========================================================
-- INSERTS (datos oficiales SRI)
-- =========================================================

INSERT INTO sri_emission_type (emission_code, emission_name) VALUES ('1','Normal')
ON CONFLICT (emission_code) DO NOTHING;

INSERT INTO sri_document_type (document_code, document_name, xml_tag) VALUES
('01','Invoice','factura'),
('03','Purchase Settlement','liquidacionCompra'),
('04','Credit Note','notaCredito'),
('05','Debit Note','notaDebito'),
('06','Delivery Guide','guiaRemision'),
('07','Withholding Certificate','comprobanteRetencion'),
('41','Withholding Presumptive Doc','docSustento'),
('42','Self/Intermediary Withholding Doc','docSustento')
ON CONFLICT (document_code) DO NOTHING;

INSERT INTO sri_environment_type (env_code, env_name) VALUES ('1','Testing'), ('2','Production')
ON CONFLICT (env_code) DO NOTHING;

INSERT INTO sri_identification_type (ident_code, ident_name) VALUES
('04','RUC'), ('05','National ID'), ('06','Passport'), ('07','Final Consumer'), ('08','Foreign Identification')
ON CONFLICT (ident_code) DO NOTHING;

INSERT INTO sri_tax_code (tax_code, tax_name) VALUES ('2','VAT'), ('3','ICE'), ('5','IRBPNR')
ON CONFLICT (tax_code) DO NOTHING;

INSERT INTO sri_vat_rate (vat_code, vat_name, rate_pct) VALUES
('0','VAT 0%',0.0000),
('2','VAT 12%',12.0000),
('3','VAT 14%',14.0000),
('4','VAT 15%',15.0000),
('5','VAT 5%',5.0000),
('6','Not Subject VAT',NULL),
('7','VAT Exempt',NULL),
('8','VAT 8% (differentiated tourism)',8.0000),
('10','VAT 13%',13.0000)
ON CONFLICT (vat_code) DO NOTHING;

INSERT INTO sri_ice_rate (ice_code, ice_name, ad_val_pct, spec_rate, rate_type, rate_note, valid_from, is_current) VALUES
('3011','ICE Cigarrillos Rubios',NULL,0.1600,'SPECIFIC','specific per unit',DATE '2020-01-01',TRUE),
('3021','ICE Cigarrillos Negros',NULL,0.1600,'SPECIFIC','specific per unit',DATE '2020-01-01',TRUE),
('3023','ICE Tabaco y sucedaneos excepto cigarrillos',150.0000,NULL,'AD_VALOREM','ad valorem 150%',DATE '2020-01-01',TRUE),
('3031','ICE Bebidas alcoholicas',75.0000,7.2500,'MIXED','ad valorem + specific',DATE '2020-01-01',TRUE),
('3041','ICE Cerveza industrial gran escala',75.0000,13.2000,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3043','ICE Cerveza artesanal',NULL,1.1500,'SPECIFIC','specific per liter',DATE '2020-01-01',TRUE),
('3033','ICE Alcohol',75.0000,7.2200,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3053','ICE Bebidas gaseosas alto azucar',NULL,NULL,'SPECIFIC','0.18 per 100g sugar',DATE '2020-01-01',TRUE),
('3054','ICE Bebidas gaseosas bajo azucar',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3073','ICE Vehiculos hasta 20000',5.0000,NULL,'AD_VALOREM','ad valorem 5%',DATE '2020-01-01',TRUE),
('3075','ICE Vehiculos 30000-40000',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3077','ICE Vehiculos 40000-50000',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3078','ICE Vehiculos 50000-60000',25.0000,NULL,'AD_VALOREM','ad valorem 25%',DATE '2020-01-01',TRUE),
('3079','ICE Vehiculos 60000-70000',30.0000,NULL,'AD_VALOREM','ad valorem 30%',DATE '2020-01-01',TRUE),
('3080','ICE Vehiculos >70000',35.0000,NULL,'AD_VALOREM','ad valorem 35%',DATE '2020-01-01',TRUE),
('3081','ICE Aviones tricars yates barcos',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3092','ICE Television prepagada',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3093','ICE Telefonia sociedades',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3101','ICE Bebidas energizantes',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3111','ICE Bebidas no alcoholicas',NULL,NULL,'SPECIFIC','0.18 per 100g sugar',DATE '2020-01-01',TRUE),
('3610','ICE Perfumes y aguas de tocador',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3620','ICE Videojuegos',35.0000,NULL,'AD_VALOREM','ad valorem 35%',DATE '2020-01-01',TRUE),
('3630','ICE Armas y municiones',300.0000,NULL,'AD_VALOREM','ad valorem 300%',DATE '2020-01-01',TRUE),
('3640','ICE Focos incandescentes',100.0000,NULL,'AD_VALOREM','ad valorem 100%',DATE '2020-01-01',TRUE),
('3660','ICE Cuotas membresias afiliaciones',35.0000,NULL,'AD_VALOREM','ad valorem 35%',DATE '2020-01-01',TRUE),
('3671','ICE Calefones y calentamiento agua gas SRI',100.0000,NULL,'AD_VALOREM','ad valorem 100%',DATE '2020-01-01',TRUE),
('3680','ICE Fundas plasticas',NULL,0.0400,'SPECIFIC','specific per unit',DATE '2020-01-01',TRUE),
('3681','ICE Telefonia movil personas naturales',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3682','ICE Consumibles tabaco calentado SRI',150.0000,NULL,'AD_VALOREM','ad valorem 150%',DATE '2020-01-01',TRUE),
('3683','ICE Consumibles tabaco calentado SENAE',150.0000,NULL,'AD_VALOREM','ad valorem 150%',DATE '2020-01-01',TRUE),
('3684','ICE Vehiculos rescate/camionetas <=30000',5.0000,NULL,'AD_VALOREM','ad valorem 5%',DATE '2020-01-01',TRUE),
('3685','ICE Vehiculos rescate/camionetas <=30000 SENAE',5.0000,NULL,'AD_VALOREM','ad valorem 5%',DATE '2020-01-01',TRUE),
('3686','ICE Vehiculos excepto rescate 20000-30000',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3687','ICE Vehiculos excepto rescate 20000-30000 SENAE',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3688','ICE Vehiculos hibridos <=35000',0.0000,NULL,'AD_VALOREM','ad valorem 0%',DATE '2020-01-01',TRUE),
('3689','ICE Vehiculos hibridos <=35000 SENAE',0.0000,NULL,'AD_VALOREM','ad valorem 0%',DATE '2020-01-01',TRUE),
('3690','ICE Vehiculos hibridos 35000-40000 SENAE',8.0000,NULL,'AD_VALOREM','ad valorem 8%',DATE '2020-01-01',TRUE),
('3691','ICE Vehiculos hibridos 35000-40000',8.0000,NULL,'AD_VALOREM','ad valorem 8%',DATE '2020-01-01',TRUE),
('3692','ICE Vehiculos hibridos 40000-50000',14.0000,NULL,'AD_VALOREM','ad valorem 14%',DATE '2020-01-01',TRUE),
('3693','ICE Vehiculos hibridos 40000-50000 SENAE',14.0000,NULL,'AD_VALOREM','ad valorem 14%',DATE '2020-01-01',TRUE),
('3694','ICE Vehiculos hibridos 50000-60000 SENAE',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3695','ICE Vehiculos hibridos 50000-60000',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3696','ICE Vehiculos hibridos 60000-70000',26.0000,NULL,'AD_VALOREM','ad valorem 26%',DATE '2020-01-01',TRUE),
('3697','ICE Vehiculos hibridos 60000-70000 SENAE',26.0000,NULL,'AD_VALOREM','ad valorem 26%',DATE '2020-01-01',TRUE),
('3698','ICE Vehiculos hibridos >70000',32.0000,NULL,'AD_VALOREM','ad valorem 32%',DATE '2020-01-01',TRUE),
('3699','ICE Vehiculos hibridos >70000 SENAE',32.0000,NULL,'AD_VALOREM','ad valorem 32%',DATE '2020-01-01',TRUE),
('3532','ICE Import alcohol SENAE',75.0000,7.2200,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3533','ICE Import bebidas alcoholicas',75.0000,7.2500,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3541','ICE Cerveza gran escala CAE',75.0000,7.2500,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3542','ICE Cigarrillos rubios CAE',NULL,0.1600,'SPECIFIC','specific per unit',DATE '2020-01-01',TRUE),
('3543','ICE Cigarrillos negros CAE',NULL,0.1600,'SPECIFIC','specific per unit',DATE '2020-01-01',TRUE),
('3544','ICE Tabaco excepto cigarrillos CAE',150.0000,NULL,'AD_VALOREM','ad valorem 150%',DATE '2020-01-01',TRUE),
('3545','ICE Cerveza artesanal SENAE',75.0000,1.5000,'MIXED','mixed',DATE '2020-01-01',TRUE),
('3552','ICE Gaseosas alto azucar SENAE',NULL,NULL,'SPECIFIC','0.18 per 100g sugar',DATE '2020-01-01',TRUE),
('3553','ICE Gaseosas bajo azucar SENAE',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3581','ICE Aeronaves CAE',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3582','ICE Avionetas/helicopteros CAE',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3601','ICE Bebidas energizantes SENAE',10.0000,NULL,'AD_VALOREM','ad valorem 10%',DATE '2020-01-01',TRUE),
('3602','ICE Bebidas no alcoholicas SENAE',NULL,NULL,'SPECIFIC','0.18 per 100g sugar',DATE '2020-01-01',TRUE),
('3710','ICE Perfumes CAE',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3720','ICE Video juegos CAE',35.0000,NULL,'AD_VALOREM','ad valorem 35%',DATE '2020-01-01',TRUE),
('3730','ICE Import armas y municiones CAE',300.0000,NULL,'AD_VALOREM','ad valorem 300%',DATE '2020-01-01',TRUE),
('3740','ICE Focos incandescentes CAE',100.0000,NULL,'AD_VALOREM','ad valorem 100%',DATE '2020-01-01',TRUE),
('3771','ICE Calefones y calentamiento agua SENAE',100.0000,NULL,'AD_VALOREM','ad valorem 100%',DATE '2020-01-01',TRUE),
('3871','ICE Vehiculos <=20000 SENAE',5.0000,NULL,'AD_VALOREM','ad valorem 5%',DATE '2020-01-01',TRUE),
('3873','ICE Vehiculos 30000-40000 SENAE',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE),
('3874','ICE Vehiculos 40000-50000 SENAE',20.0000,NULL,'AD_VALOREM','ad valorem 20%',DATE '2020-01-01',TRUE),
('3875','ICE Vehiculos 50000-60000 SENAE',25.0000,NULL,'AD_VALOREM','ad valorem 25%',DATE '2020-01-01',TRUE),
('3876','ICE Vehiculos 60000-70000 SENAE',30.0000,NULL,'AD_VALOREM','ad valorem 30%',DATE '2020-01-01',TRUE),
('3877','ICE Vehiculos >70000 SENAE',35.0000,NULL,'AD_VALOREM','ad valorem 35%',DATE '2020-01-01',TRUE),
('3878','ICE Aviones tricars yates SENAE',15.0000,NULL,'AD_VALOREM','ad valorem 15%',DATE '2020-01-01',TRUE)
ON CONFLICT (ice_code) DO NOTHING;

INSERT INTO sri_withholding_tax_type (wh_tax_code, wh_tax_name) VALUES
('1','Income Tax'), ('2','VAT'), ('4','Presumptive VAT and Income'), ('6','ISD')
ON CONFLICT (wh_tax_code) DO NOTHING;

INSERT INTO sri_vat_withholding_rate (vat_wh_code, rate_pct, rate_name) VALUES
('1',30.0000,'VAT withholding 30%'),
('2',70.0000,'VAT withholding 70%'),
('3',100.0000,'VAT withholding 100%'),
('7',0.0000,'VAT withholding zero'),
('8',0.0000,'VAT withholding not applicable'),
('9',10.0000,'VAT withholding 10%'),
('10',20.0000,'VAT withholding 20%'),
('11',50.0000,'VAT withholding 50%')
ON CONFLICT (vat_wh_code) DO NOTHING;

INSERT INTO sri_isd_withholding_rate (isd_wh_code, rate_pct, valid_from, valid_to, is_current) VALUES
('4580',5.0000,DATE '2013-03-01',DATE '2025-12-31',FALSE),
('4580',5.0000,DATE '2026-01-01',NULL,TRUE)
ON CONFLICT (isd_wh_code, valid_from) DO NOTHING;

INSERT INTO sri_presumptive_vat_withholding_rate (pres_vat_code, rate_pct, rate_name) VALUES
('3',100.0000,'Presumptive VAT withholding 100%'),
('4',12.0000,'Editors to hawkers margin withholding'),
('5',100.0000,'Newspaper/magazine to distributors'),
('6',100.0000,'Newspaper/magazine to hawkers')
ON CONFLICT (pres_vat_code) DO NOTHING;

INSERT INTO sri_oil_derivatives_income_withholding (oil_inc_code, rate_pct, rate_name) VALUES
('327',0.002000,'Income withholding 2 per thousand'),
('328',0.003000,'Income withholding 3 per thousand')
ON CONFLICT (oil_inc_code) DO NOTHING;

INSERT INTO sri_payment_method (payment_code, payment_name, start_date, end_date) VALUES
('01','No financial system usage',DATE '2013-01-01',NULL),
('15','Debt compensation',DATE '2013-01-01',NULL),
('16','Debit card',DATE '2016-06-01',NULL),
('17','Electronic money',DATE '2016-06-01',NULL),
('18','Prepaid card',DATE '2016-06-01',NULL),
('19','Credit card',DATE '2016-06-01',NULL),
('20','Other with financial system usage',DATE '2016-06-01',NULL),
('21','Endorsement of securities',DATE '2016-06-01',NULL)
ON CONFLICT (payment_code) DO NOTHING;

INSERT INTO sri_document_status (status_code, status_name) VALUES
('PPR','In processing'), ('AUT','Authorized'), ('NAT','Not authorized')
ON CONFLICT (status_code) DO NOTHING;

-- Países (solo una muestra representativa; agregar todos los necesarios)
INSERT INTO sri_country (country_code, country_name) VALUES
('032','ARGENTINA'), ('068','BOLIVIA'), ('076','BRAZIL'), ('152','CHILE'),
('170','COLOMBIA'), ('218','ECUADOR'), ('600','PARAGUAY'), ('604','PERU'),
('858','URUGUAY'), ('862','VENEZUELA'), ('840','UNITED STATES'), ('724','SPAIN'),
('593','ECUADOR'), ('594','AGUAS INTERNACIONALES')
ON CONFLICT (country_code) DO NOTHING;

INSERT INTO sri_supporting_document (support_code, support_name) VALUES
('01','Invoice'), ('04','Credit Note'), ('05','Debit Note'), ('06','Delivery Guide'),
('07','Withholding Certificate'), ('41','Withholding Presumptive Support'),
('42','Self/Intermediary Withholding Support')
ON CONFLICT (support_code) DO NOTHING;

INSERT INTO sri_reimbursement_provider_type (provider_code, provider_name) VALUES
('01','Domestic provider'), ('02','Foreign provider')
ON CONFLICT (provider_code) DO NOTHING;

INSERT INTO sri_error_code (error_code, error_type, error_name, error_desc, source_area) VALUES
('35','ERROR','Invalid document','XML schema validation failed','RECEPCION'),
('39','ERROR','Invalid signature','Issuer electronic signature invalid','AUTORIZACION'),
('43','ERROR','Access key already registered','Access key already exists','RECEPCION'),
('45','ERROR','Sequential already registered','Sequential already exists','RECEPCION'),
('52','ERROR','Difference validation error','Calculation mismatch in document','AUTORIZACION'),
('59','WARNING','Identification not found','Buyer identification does not exist','EMISOR'),
('65','ERROR','Late issue date','Out-of-time emission by emission type','EMISOR'),
('70','ERROR','Access key in process','Same key still processing','RECEPCION'),
('2000','DIG','SUCCESS','Success','DIG')
ON CONFLICT (error_code) DO NOTHING;

INSERT INTO sri_regime_type (regime_code, regime_name) VALUES
('GEN','General Regime'), ('RIM1','RIMPE Entrepreneur'), ('RIM2','RIMPE Popular Business'), ('EXT','Foreign Regime')
ON CONFLICT (regime_code) DO NOTHING;

INSERT INTO sri_tax_support_code (support_tax_code, support_tax_name) VALUES
('01','Domestic purchase with invoice'),
('02','Import goods'),
('03','Import services'),
('04','Local purchase with withholding'),
('05','Purchase to final consumer'),
('06','Reimbursement operation'),
('07','Purchase settlement'),
('08','Exports and assimilated'),
('09','Tax credit not applicable'),
('10','Other tax support')
ON CONFLICT (support_tax_code) DO NOTHING;
