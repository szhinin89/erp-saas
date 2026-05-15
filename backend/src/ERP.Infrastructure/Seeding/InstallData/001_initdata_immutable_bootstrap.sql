-- Immutable bootstrap seed: Ecuador geographic base catalogs.
-- Source: INEC (Instituto Nacional de Estadistica y Censos), via ArcGIS layers
--         published in Ecuador en Cifras (service metadata references INEC geoportal).
-- Generated with: erp-saas/scripts/import_inec_ecuador_geography.ps1
-- (No BEGIN/COMMIT: EF migration already runs inside a transaction.)

-- Canonical country source: sri_country
INSERT INTO sri_country (code, iso2, name, phone_code, is_active)
VALUES ('ECU', 'EC', 'Ecuador', '+593', TRUE)
ON CONFLICT (code) DO UPDATE
SET iso2 = EXCLUDED.iso2,
    name = EXCLUDED.name,
    phone_code = EXCLUDED.phone_code,
    is_active = EXCLUDED.is_active;

-- geo_countries is derived from sri_country for geography FK compatibility.
INSERT INTO geo_countries (id, name)
SELECT 'EC', sc.name
FROM sri_country sc
WHERE sc.code = 'ECU'
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

-- INEC hierarchy level 1: provinces.
-- Used for country-level territorial segmentation in addresses and reporting.
INSERT INTO geo_provinces (id, country_id, name) VALUES ('01', 'EC', 'AZUAY')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('02', 'EC', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('03', 'EC', 'CAÑAR')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('04', 'EC', 'CARCHI')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('05', 'EC', 'COTOPAXI')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('06', 'EC', 'CHIMBORAZO')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('07', 'EC', 'EL ORO')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('08', 'EC', 'ESMERALDAS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('09', 'EC', 'GUAYAS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('10', 'EC', 'IMBABURA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('11', 'EC', 'LOJA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('12', 'EC', 'LOS RIOS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('13', 'EC', 'MANABI')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('14', 'EC', 'MORONA SANTIAGO')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('15', 'EC', 'NAPO')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('16', 'EC', 'PASTAZA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('17', 'EC', 'PICHINCHA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('18', 'EC', 'TUNGURAHUA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('19', 'EC', 'ZAMORA CHINCHIPE')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('20', 'EC', 'GALAPAGOS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('21', 'EC', 'SUCUMBIOS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('22', 'EC', 'ORELLANA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('23', 'EC', 'SANTO DOMINGO DE LOS TSACHILAS')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('24', 'EC', 'SANTA ELENA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;
INSERT INTO geo_provinces (id, country_id, name) VALUES ('90', 'EC', 'ZONA NO DELIMITADA')
ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;

-- INEC hierarchy level 2: cantons.
-- Used for municipality-level segmentation under each province.
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0101', '01', 'CUENCA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0102', '01', 'GIRON')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0103', '01', 'GUALACEO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0104', '01', 'NABON')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0105', '01', 'PAUTE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0106', '01', 'PUCARA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0107', '01', 'SAN FERNANDO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0108', '01', 'SANTA ISABEL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0109', '01', 'SIGSIG')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0110', '01', 'OÑA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0111', '01', 'CHORDELEG')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0112', '01', 'EL PAN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0113', '01', 'SEVILLA DE ORO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0114', '01', 'GUACHAPALA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0115', '01', 'CAMILO PONCE ENRIQUEZ')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0201', '02', 'GUARANDA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0202', '02', 'CHILLANES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0203', '02', 'CHIMBO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0204', '02', 'ECHEANDIA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0205', '02', 'SAN MIGUEL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0206', '02', 'CALUMA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0207', '02', 'LAS NAVES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0301', '03', 'AZOGUES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0302', '03', 'BIBLIAN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0303', '03', 'CAÑAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0304', '03', 'LA TRONCAL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0305', '03', 'EL TAMBO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0306', '03', 'DELEG')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0307', '03', 'SUSCAL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0401', '04', 'TULCAN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0402', '04', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0403', '04', 'ESPEJO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0404', '04', 'MIRA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0405', '04', 'MONTUFAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0406', '04', 'SAN PEDRO DE HUACA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0501', '05', 'LATACUNGA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0502', '05', 'LA MANA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0503', '05', 'PANGUA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0504', '05', 'PUJILI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0505', '05', 'SALCEDO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0506', '05', 'SAQUISILI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0507', '05', 'SIGCHOS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0601', '06', 'RIOBAMBA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0602', '06', 'ALAUSI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0603', '06', 'COLTA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0604', '06', 'CHAMBO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0605', '06', 'CHUNCHI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0606', '06', 'GUAMOTE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0607', '06', 'GUANO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0608', '06', 'PALLATANGA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0609', '06', 'PENIPE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0610', '06', 'CUMANDA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0701', '07', 'MACHALA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0702', '07', 'ARENILLAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0703', '07', 'ATAHUALPA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0704', '07', 'BALSAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0705', '07', 'CHILLA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0706', '07', 'EL GUABO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0707', '07', 'HUAQUILLAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0708', '07', 'MARCABELI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0709', '07', 'PASAJE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0710', '07', 'PIÑAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0711', '07', 'PORTOVELO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0712', '07', 'SANTA ROSA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0713', '07', 'ZARUMA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0714', '07', 'LAS LAJAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0801', '08', 'ESMERALDAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0802', '08', 'ELOY ALFARO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0803', '08', 'MUISNE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0804', '08', 'QUININDE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0805', '08', 'SAN LORENZO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0806', '08', 'ATACAMES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0807', '08', 'RIOVERDE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0808', '08', 'LA CONCORDIA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0901', '09', 'GUAYAQUIL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0902', '09', 'ALFREDO BAQUERIZO MORENO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0903', '09', 'BALAO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0904', '09', 'BALZAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0905', '09', 'COLIMES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0906', '09', 'DAULE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0907', '09', 'DURAN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0908', '09', 'EMPALME')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0909', '09', 'EL TRIUNFO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0910', '09', 'MILAGRO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0911', '09', 'NARANJAL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0912', '09', 'NARANJITO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0913', '09', 'PALESTINA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0914', '09', 'PEDRO CARBO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0916', '09', 'SAMBORONDON')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0918', '09', 'SANTA LUCIA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0919', '09', 'SALITRE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0920', '09', 'SAN JACINTO DE YAGUACHI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0921', '09', 'PLAYAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0922', '09', 'SIMON BOLIVAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0923', '09', 'CRNEL. MARCELINO MARIDUEÑA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0924', '09', 'LOMAS DE SARGENTILLO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0925', '09', 'NOBOL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0927', '09', 'GNRAL. ANTONIO ELIZALDE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('0928', '09', 'ISIDRO AYORA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1001', '10', 'IBARRA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1002', '10', 'ANTONIO ANTE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1003', '10', 'COTACACHI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1004', '10', 'OTAVALO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1005', '10', 'PIMAMPIRO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1006', '10', 'SAN MIGUEL DE URCUQUI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1101', '11', 'LOJA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1102', '11', 'CALVAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1103', '11', 'CATAMAYO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1104', '11', 'CELICA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1105', '11', 'CHAGUARPAMBA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1106', '11', 'ESPINDOLA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1107', '11', 'GONZANAMA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1108', '11', 'MACARA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1109', '11', 'PALTAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1110', '11', 'PUYANGO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1111', '11', 'SARAGURO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1112', '11', 'SOZORANGA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1113', '11', 'ZAPOTILLO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1114', '11', 'PINDAL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1115', '11', 'QUILANGA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1116', '11', 'OLMEDO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1201', '12', 'BABAHOYO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1202', '12', 'BABA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1203', '12', 'MONTALVO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1204', '12', 'PUEBLOVIEJO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1205', '12', 'QUEVEDO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1206', '12', 'URDANETA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1207', '12', 'VENTANAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1208', '12', 'VINCES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1209', '12', 'PALENQUE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1210', '12', 'BUENA FE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1211', '12', 'VALENCIA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1212', '12', 'MOCACHE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1213', '12', 'QUINSALOMA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1301', '13', 'PORTOVIEJO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1302', '13', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1303', '13', 'CHONE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1304', '13', 'EL CARMEN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1305', '13', 'FLAVIO ALFARO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1306', '13', 'JIPIJAPA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1307', '13', 'JUNIN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1308', '13', 'MANTA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1309', '13', 'MONTECRISTI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1310', '13', 'PAJAN')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1311', '13', 'PICHINCHA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1312', '13', 'ROCAFUERTE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1313', '13', 'SANTA ANA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1314', '13', 'SUCRE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1315', '13', 'TOSAGUA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1316', '13', '24 DE MAYO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1317', '13', 'PEDERNALES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1318', '13', 'OLMEDO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1319', '13', 'PUERTO LOPEZ')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1320', '13', 'JAMA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1321', '13', 'JARAMIJO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1322', '13', 'SAN VICENTE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1401', '14', 'MORONA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1402', '14', 'GUALAQUIZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1403', '14', 'LIMON INDANZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1404', '14', 'PALORA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1405', '14', 'SANTIAGO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1406', '14', 'SUCUA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1407', '14', 'HUAMBOYA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1408', '14', 'SAN JUAN BOSCO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1409', '14', 'TAISHA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1410', '14', 'LOGROÐO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1411', '14', 'PABLO SEXTO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1412', '14', 'TIWINTZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1501', '15', 'TENA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1503', '15', 'ARCHIDONA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1504', '15', 'EL CHACO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1507', '15', 'QUIJOS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1509', '15', 'CARLOS JULIO AROSEMENA TOLA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1601', '16', 'PASTAZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1602', '16', 'MERA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1603', '16', 'SANTA CLARA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1604', '16', 'ARAJUNO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1701', '17', 'QUITO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1702', '17', 'CAYAMBE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1703', '17', 'MEJIA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1704', '17', 'PEDRO MONCAYO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1705', '17', 'RUMIÐAHUI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1707', '17', 'SAN MIGUEL DE LOS BANCOS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1708', '17', 'PEDRO VICENTE MALDONADO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1709', '17', 'PUERTO QUITO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1801', '18', 'AMBATO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1802', '18', 'BAÑOS DE AGUA SANTA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1803', '18', 'CEVALLOS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1804', '18', 'MOCHA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1805', '18', 'PATATE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1806', '18', 'QUERO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1807', '18', 'SAN PEDRO DE PELILEO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1808', '18', 'SANTIAGO DE PILLARO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1809', '18', 'TISALEO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1901', '19', 'ZAMORA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1902', '19', 'CHINCHIPE')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1903', '19', 'NANGARITZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1904', '19', 'YACUAMBI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1905', '19', 'YANTZAZA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1906', '19', 'EL PANGUI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1907', '19', 'CENTINELA DEL CONDOR')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1908', '19', 'PALANDA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('1909', '19', 'PAQUISHA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2001', '20', 'SAN CRISTOBAL')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2002', '20', 'ISABELA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2003', '20', 'SANTA CRUZ')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2101', '21', 'LAGO AGRIO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2102', '21', 'GONZALO PIZARRO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2103', '21', 'PUTUMAYO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2104', '21', 'SHUSHUFINDI')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2105', '21', 'SUCUMBIOS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2106', '21', 'CASCALES')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2107', '21', 'CUYABENO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2201', '22', 'ORELLANA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2202', '22', 'AGUARICO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2203', '22', 'LA JOYA DE LOS SACHAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2204', '22', 'LORETO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2301', '23', 'SANTO DOMINGO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2401', '24', 'SANTA ELENA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2402', '24', 'LA LIBERTAD')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('2403', '24', 'SALINAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('9001', '90', 'LAS GOLONDRINAS')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('9003', '90', 'MANGA DEL CURA')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;
INSERT INTO geo_cantons (id, province_id, name) VALUES ('9004', '90', 'EL PIEDRERO')
ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;

-- INEC hierarchy level 3: parishes.
-- Used for fine-grained locality references beneath each canton.
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010150', '0101', 'CUENCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010151', '0101', 'BAÑOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010152', '0101', 'CUMBE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010153', '0101', 'CHAUCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010154', '0101', 'CHECA (JIDCAY)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010155', '0101', 'CHIQUINTAD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010156', '0101', 'LLACAO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010157', '0101', 'MOLLETURO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010158', '0101', 'NULTI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010159', '0101', 'OCTAVIO CORDERO PALACIOS (STA. ROSA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010160', '0101', 'PACCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010161', '0101', 'QUINGEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010162', '0101', 'RICAURTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010163', '0101', 'SAN JOAQUIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010164', '0101', 'SANTA ANA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010165', '0101', 'SAYAUSI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010166', '0101', 'SIDCAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010167', '0101', 'SININCAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010168', '0101', 'TARQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010169', '0101', 'TURI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010170', '0101', 'VALLE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010171', '0101', 'VICTORIA DEL PORTETE (IRQUIS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010250', '0102', 'GIRON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010251', '0102', 'ASUNCION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010252', '0102', 'SAN GERARDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010350', '0103', 'GUALACEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010352', '0103', 'DANIEL CORDOVA TORAL (EL ORIENTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010353', '0103', 'JADAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010354', '0103', 'MARIANO MORENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010356', '0103', 'REMIGIO CRESPO TORAL (GULAG)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010357', '0103', 'SAN JUAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010358', '0103', 'ZHIDMAD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010359', '0103', 'LUIS CORDERO VEGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010360', '0103', 'SIMON BOLIVAR (CAB. EN GAÑANZOL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010450', '0104', 'NABON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010451', '0104', 'COCHAPATA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010452', '0104', 'EL PROGRESO (CAB. EN ZHOTA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010453', '0104', 'LAS NIEVES (CHAYA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010550', '0105', 'PAUTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010552', '0105', 'BULAN (JOSE VICTOR IZQUIERDO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010553', '0105', 'CHICAN (GUILLERMO ORTEGA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010554', '0105', 'EL CABO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010556', '0105', 'GUARAINAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010559', '0105', 'SAN CRISTOBAL (CARLOS ORDOÑEZ LAZO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010561', '0105', 'TOMEBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010562', '0105', 'DUG DUG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010650', '0106', 'PUCARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010652', '0106', 'SAN RAFAEL DE SHARUG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010750', '0107', 'SAN FERNANDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010751', '0107', 'CHUMBLIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010850', '0108', 'SANTA ISABEL (CHAGUARURCO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010851', '0108', 'ABDON CALDERON (LA UNION)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010853', '0108', 'ZHAGLLI (SHAGLLI )')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010854', '0108', 'SAN SALVADOR DE CAÑARIBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010950', '0109', 'SIGSIG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010951', '0109', 'CUCHIL (CUTCHIL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010952', '0109', 'GIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010953', '0109', 'GUEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010954', '0109', 'LUDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010955', '0109', 'SAN BARTOLOME')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('010956', '0109', 'SAN JOSE DE RARANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011050', '0110', 'SAN FELIPE DE OÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011051', '0110', 'SUSUDEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011150', '0111', 'CHORDELEG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011151', '0111', 'PRINCIPAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011152', '0111', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011153', '0111', 'LUIS GALARZA ORELLANA (CAB. EN DELEGSOL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011154', '0111', 'SAN MARTIN DE PUZHIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011250', '0112', 'EL PAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011253', '0112', 'SAN VICENTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011350', '0113', 'SEVILLA DE ORO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011351', '0113', 'AMALUZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011352', '0113', 'PALMAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011450', '0114', 'GUACHAPALA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011550', '0115', 'CAMILO PONCE ENRIQUEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('011551', '0115', 'EL CARMEN DE PIJILI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020150', '0201', 'GUARANDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020151', '0201', 'FACUNDO VELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020153', '0201', 'JULIO E. MORENO (CATANAHUAN GRANDE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020155', '0201', 'SALINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020156', '0201', 'SAN LORENZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020157', '0201', 'SAN SIMON (YACOTO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020158', '0201', 'SANTA FE (SANTA FE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020159', '0201', 'SIMIATUG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020160', '0201', 'SAN LUIS DE PAMBIL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020250', '0202', 'CHILLANES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020251', '0202', 'SAN JOSE DEL TAMBO (TAMBOPAMBA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020350', '0203', 'SAN JOSE DE CHIMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020351', '0203', 'ASUNCION (ASANCOTO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020353', '0203', 'MAGDALENA (CHAPACOTO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020354', '0203', 'SAN SEBASTIAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020355', '0203', 'TELIMBELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020450', '0204', 'ECHEANDIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020550', '0205', 'SAN MIGUEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020551', '0205', 'BALSAPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020552', '0205', 'BILOVAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020553', '0205', 'REGULO DE MORA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020554', '0205', 'SAN PABLO (SAN PABLO DE ATENAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020555', '0205', 'SANTIAGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020556', '0205', 'SAN VICENTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020650', '0206', 'CALUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('020750', '0207', 'LAS NAVES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030150', '0301', 'AZOGUES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030151', '0301', 'COJITAMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030153', '0301', 'GUAPAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030154', '0301', 'JAVIER LOYOLA (CHUQUIPATA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030155', '0301', 'LUIS CORDERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030156', '0301', 'PINDILIG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030157', '0301', 'RIVERA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030158', '0301', 'SAN MIGUEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030160', '0301', 'TADAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030250', '0302', 'BIBLIAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030251', '0302', 'NAZON (CAB. EN PAMPA DE DOMINGUEZ)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030252', '0302', 'SAN FRANCISCO DE SAGEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030253', '0302', 'TURUPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030254', '0302', 'JERUSALEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030350', '0303', 'CAÑAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030351', '0303', 'CHONTAMARCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030352', '0303', 'CHOROCOPTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030353', '0303', 'GENERAL MORALES (SOCARTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030354', '0303', 'GUALLETURO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030355', '0303', 'HONORATO VASQUEZ (TAMBO VIEJO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030356', '0303', 'INGAPIRCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030357', '0303', 'JUNCAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030358', '0303', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030361', '0303', 'ZHUD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030362', '0303', 'VENTURA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030363', '0303', 'DUCUR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030450', '0304', 'LA TRONCAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030451', '0304', 'MANUEL J. CALLE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030452', '0304', 'PANCHO NEGRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030550', '0305', 'EL TAMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030650', '0306', 'DELEG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030651', '0306', 'SOLANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('030750', '0307', 'SUSCAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040150', '0401', 'TULCAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040151', '0401', 'EL CARMELO (EL PUN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040153', '0401', 'JULIO ANDRADE (OREJUELA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040154', '0401', 'MALDONADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040155', '0401', 'PIOTER')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040156', '0401', 'TOBAR DONOSO (LA BOCANA DE CAMUMBI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040157', '0401', 'TUFIÑO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040158', '0401', 'URBINA (TAYA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040159', '0401', 'EL CHICAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040161', '0401', 'SANTA MARTHA DE CUBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040250', '0402', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040251', '0402', 'GARCIA MORENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040252', '0402', 'LOS ANDES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040253', '0402', 'MONTE OLIVO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040254', '0402', 'SAN VICENTE DE PUSIR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040255', '0402', 'SAN RAFAEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040350', '0403', 'EL ANGEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040351', '0403', 'EL GOALTAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040352', '0403', 'LA LIBERTAD (ALIZO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040353', '0403', 'SAN ISIDRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040450', '0404', 'MIRA (CHONTAHUASI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040451', '0404', 'CONCEPCION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040452', '0404', 'JIJON Y CAAMAÑO (CAB. EN RIO BLANCO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040453', '0404', 'JUAN MONTALVO (SAN IGNACIO DE QUIL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040550', '0405', 'SAN GABRIEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040551', '0405', 'CRISTOBAL COLON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040552', '0405', 'CHITAN DE NAVARRETE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040553', '0405', 'FERNANDEZ SALVADOR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040554', '0405', 'LA PAZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040555', '0405', 'PIARTAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040650', '0406', 'HUACA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('040651', '0406', 'MARISCAL SUCRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050150', '0501', 'LATACUNGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050151', '0501', 'ALAQUES (ALAQUEZ)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050152', '0501', 'BELISARIO QUEVEDO (GUANAILIN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050153', '0501', 'GUAITACAMA (GUAYTACAMA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050154', '0501', 'JOSEGUANGO BAJO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050156', '0501', 'MULALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050157', '0501', '11 DE NOVIEMBRE (ILINCHISI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050158', '0501', 'POALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050159', '0501', 'SAN JUAN DE PASTOCALLE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050161', '0501', 'TANICUCHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050162', '0501', 'TOACASO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050250', '0502', 'LA MANA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050251', '0502', 'GUASAGANDA (CAB. EN GUASAGANDA CENTRO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050252', '0502', 'PUCAYACU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050350', '0503', 'EL CORAZON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050351', '0503', 'MORASPUNGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050352', '0503', 'PINLLOPATA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050353', '0503', 'RAMON CAMPAÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050450', '0504', 'PUJILI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050451', '0504', 'ANGAMARCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050453', '0504', 'GUANGAJE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050455', '0504', 'LA VICTORIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050456', '0504', 'PILALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050457', '0504', 'TINGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050458', '0504', 'ZUMBAHUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050550', '0505', 'SAN MIGUEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050551', '0505', 'ANTONIO JOSE HOLGUIN (SANTA LUCIA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050552', '0505', 'CUSUBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050553', '0505', 'MULALILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050554', '0505', 'MULLIQUINDIL (SANTA ANA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050555', '0505', 'PANSALEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050650', '0506', 'SAQUISILI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050651', '0506', 'CANCHAGUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050652', '0506', 'CHANTILIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050653', '0506', 'COCHAPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050750', '0507', 'SIGCHOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050751', '0507', 'CHUGCHILLAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050752', '0507', 'ISINLIVI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050753', '0507', 'LAS PAMPAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('050754', '0507', 'PALO QUEMADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060150', '0601', 'RIOBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060151', '0601', 'CACHA (CAB. EN MACHANGARA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060152', '0601', 'CALPI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060153', '0601', 'CUBIJIES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060154', '0601', 'FLORES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060155', '0601', 'LICAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060156', '0601', 'LICTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060157', '0601', 'PUNGALA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060158', '0601', 'PUNIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060159', '0601', 'QUIMIAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060160', '0601', 'SAN JUAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060161', '0601', 'SAN LUIS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060250', '0602', 'ALAUSI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060251', '0602', 'ACHUPALLAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060253', '0602', 'GUASUNTOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060254', '0602', 'HUIGRA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060255', '0602', 'MULTITUD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060256', '0602', 'PISTISHI (NARIZ DEL DIABLO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060257', '0602', 'PUMALLACTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060258', '0602', 'SEVILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060259', '0602', 'SIBAMBE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060260', '0602', 'TIXAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060350', '0603', 'VILLA LA UNION (CAJABAMBA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060351', '0603', 'CAÑI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060352', '0603', 'COLUMBE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060353', '0603', 'JUAN DE VELASCO (PANGOR)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060354', '0603', 'SANTIAGO DE QUITO (CAB. EN SAN ANTONIO DE QUITO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060450', '0604', 'CHAMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060550', '0605', 'CHUNCHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060551', '0605', 'CAPZOL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060552', '0605', 'COMPUD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060553', '0605', 'GONZOL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060554', '0605', 'LLAGOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060650', '0606', 'GUAMOTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060651', '0606', 'CEBADAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060652', '0606', 'PALMIRA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060750', '0607', 'GUANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060751', '0607', 'GUANANDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060752', '0607', 'ILAPO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060753', '0607', 'LA PROVIDENCIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060754', '0607', 'SAN ANDRES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060755', '0607', 'SAN GERARDO DE PACAICAGUAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060756', '0607', 'SAN ISIDRO DE PATULU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060757', '0607', 'SAN JOSE DEL CHAZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060758', '0607', 'SANTA FE DE GALAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060759', '0607', 'VALPARAISO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060850', '0608', 'PALLATANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060950', '0609', 'PENIPE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060951', '0609', 'EL ALTAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060952', '0609', 'MATUS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060953', '0609', 'PUELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060954', '0609', 'SAN ANTONIO DE BAYUSHIG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060955', '0609', 'LA CANDELARIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('060956', '0609', 'BILBAO (CAB. EN QUILLUYACU)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('061050', '0610', 'CUMANDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070150', '0701', 'MACHALA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070152', '0701', 'EL RETIRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070250', '0702', 'ARENILLAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070251', '0702', 'CHACRAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070254', '0702', 'PALMALES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070255', '0702', 'CARCABON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070350', '0703', 'PACCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070351', '0703', 'AYAPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070352', '0703', 'CORDONCILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070353', '0703', 'MILAGRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070354', '0703', 'SAN JOSE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070355', '0703', 'SAN JUAN DE CERRO AZUL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070450', '0704', 'BALSAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070451', '0704', 'BELLAMARIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070550', '0705', 'CHILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070650', '0706', 'EL GUABO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070651', '0706', 'BARBONES (SUCRE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070652', '0706', 'LA IBERIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070653', '0706', 'TENDALES (CAB. EN PUERTO TENDALES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070654', '0706', 'RIO BONITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070750', '0707', 'HUAQUILLAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070850', '0708', 'MARCABELI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070851', '0708', 'EL INGENIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070950', '0709', 'PASAJE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070951', '0709', 'BUENAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070952', '0709', 'CASACAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070953', '0709', 'LA PEAÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070954', '0709', 'PROGRESO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070955', '0709', 'UZHCURRUMI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('070956', '0709', 'CAÑAQUEMADA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071050', '0710', 'PIÑAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071051', '0710', 'CAPIRO (CAB. EN LA CAPILLA DE CAPIRO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071052', '0710', 'LA BOCANA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071053', '0710', 'MOROMORO (CAB. EN EL VADO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071054', '0710', 'PIEDRAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071055', '0710', 'SAN ROQUE (AMBROSIO MALDONADO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071056', '0710', 'SARACAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071150', '0711', 'PORTOVELO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071151', '0711', 'CURTINCAPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071152', '0711', 'MORALES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071153', '0711', 'SALATI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071250', '0712', 'SANTA ROSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071251', '0712', 'BELLAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071252', '0712', 'JAMBELI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071253', '0712', 'LA AVANZADA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071254', '0712', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071255', '0712', 'TORATA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071256', '0712', 'VICTORIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071257', '0712', 'BELLAMARIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071350', '0713', 'ZARUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071351', '0713', 'ABAÑIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071352', '0713', 'ARCAPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071353', '0713', 'GUANAZAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071354', '0713', 'GUIZHAGUIÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071355', '0713', 'HUERTAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071356', '0713', 'MALVAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071357', '0713', 'MULUNCAY GRANDE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071358', '0713', 'SINSAO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071359', '0713', 'SALVIAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071450', '0714', 'LA VICTORIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071451', '0714', 'LA LIBERTAD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071452', '0714', 'EL PARAISO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('071453', '0714', 'SAN ISIDRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080150', '0801', 'ESMERALDAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080152', '0801', 'CAMARONES (CAB. EN SAN VICENTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080153', '0801', 'CRNEL. CARLOS CONCHA TORRES (CAB. EN HUELE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080154', '0801', 'CHINCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080159', '0801', 'MAJUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080163', '0801', 'SAN MATEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080165', '0801', 'TABIAZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080166', '0801', 'TACHINA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080168', '0801', 'VUELTA LARGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080250', '0802', 'VALDEZ (LIMONES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080251', '0802', 'ANCHAYACU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080252', '0802', 'ATAHUALPA (CAB. EN CAMARONES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080253', '0802', 'BORBON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080254', '0802', 'LA TOLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080255', '0802', 'LUIS VARGAS TORRES (CAB. EN PLAYA DE ORO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080256', '0802', 'MALDONADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080257', '0802', 'PAMPANAL DE BOLIVAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080258', '0802', 'SAN FRANCISCO DE ONZOLE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080259', '0802', 'SANTO DOMINGO DE ONZOLE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080260', '0802', 'SELVA ALEGRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080261', '0802', 'TELEMBI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080262', '0802', 'COLON ELOY DEL MARIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080263', '0802', 'SAN JOSE DE CAYAPAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080264', '0802', 'TIMBIRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080265', '0802', 'SANTA LUCIA DE LAS PEÑAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080350', '0803', 'MUISNE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080351', '0803', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080352', '0803', 'DAULE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080353', '0803', 'GALERA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080354', '0803', 'QUINGUE (OLMEDO PERDOMO FRANCO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080355', '0803', 'SALIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080356', '0803', 'SAN FRANCISCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080357', '0803', 'SAN GREGORIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080358', '0803', 'SAN JOSE DE CHAMANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080450', '0804', 'ROSA ZARATE (QUININDE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080451', '0804', 'CUBE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080452', '0804', 'CHURA (CHANCAMA) (CAB. EN EL YERBERO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080453', '0804', 'MALIMPIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080454', '0804', 'VICHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080455', '0804', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080550', '0805', 'SAN LORENZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080551', '0805', 'ALTO TAMBO (CAB EN GUADUAL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080552', '0805', 'ANCON (PICHANGAL) (CAB. EN PALMA REAL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080553', '0805', 'CALDERON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080554', '0805', 'CARONDELET')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080555', '0805', '5 DE JUNIO (CAB. EN UIMBI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080556', '0805', 'CONCEPCION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080557', '0805', 'MATAJE (CAB. EN SANTANDER)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080558', '0805', 'SAN JAVIER DE CACHAVI (CAB. EN SAN JAVIER)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080559', '0805', 'SANTA RITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080560', '0805', 'TAMBILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080561', '0805', 'TULULBI (CAB. EN RICAURTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080562', '0805', 'URBINA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080650', '0806', 'ATACAMES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080651', '0806', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080652', '0806', 'SUA (CAB. EN LA BOCANA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080653', '0806', 'TONCHIGUE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080654', '0806', 'TONSUPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080750', '0807', 'RIOVERDE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080751', '0807', 'CHONTADURO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080752', '0807', 'CHUMUNDE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080753', '0807', 'LAGARTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080754', '0807', 'MONTALVO (CAB EN HORQUETA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080755', '0807', 'ROCAFUERTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080850', '0808', 'LA CONCORDIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080851', '0808', 'MONTERREY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080852', '0808', 'LA VILLEGAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('080853', '0808', 'PLAN PILOTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090150', '0901', 'GUAYAQUIL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090152', '0901', 'JUAN GOMEZ RENDON (PROGRESO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090153', '0901', 'MORRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090156', '0901', 'POSORJA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090157', '0901', 'PUNA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090158', '0901', 'TENGUEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090250', '0902', 'ALFREDO BAQUERIZO MORENO (JUJAN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090350', '0903', 'BALAO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090450', '0904', 'BALZAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090550', '0905', 'COLIMES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090551', '0905', 'SAN JACINTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090650', '0906', 'DAULE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090652', '0906', 'JUAN BAUTISTA AGUIRRE (LOS TINTOS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090653', '0906', 'LAUREL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090654', '0906', 'LIMONAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090656', '0906', 'LOS LOJAS (ENRIQUE BAQUERIZO MORENO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090750', '0907', 'ELOY ALFARO (DURAN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090850', '0908', 'VELASCO IBARRA (CAB. EL EMPALME)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090851', '0908', 'GUAYAS (PUEBLO NUEVO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090852', '0908', 'EL ROSARIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('090950', '0909', 'EL TRIUNFO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091050', '0910', 'MILAGRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091051', '0910', 'CHOBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091053', '0910', 'MARISCAL SUCRE (HUAQUES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091054', '0910', 'ROBERTO ASTUDILLO (CAB. EN CRUCE DE VENECIA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091150', '0911', 'NARANJAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091151', '0911', 'JESUS MARIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091152', '0911', 'SAN CARLOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091153', '0911', 'SANTA ROSA DE FLANDES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091154', '0911', 'TAURA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091250', '0912', 'NARANJITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091350', '0913', 'PALESTINA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091450', '0914', 'PEDRO CARBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091451', '0914', 'VALLE DE LA VIRGEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091452', '0914', 'SABANILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091650', '0916', 'SAMBORONDON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091651', '0916', 'TARIFA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091850', '0918', 'SANTA LUCIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091950', '0919', 'EL SALITRE (LAS RAMAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091951', '0919', 'GRAL. VERNAZA (DOS ESTEROS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091952', '0919', 'LA VICTORIA (ÑAUZA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('091953', '0919', 'JUNQUILLAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092050', '0920', 'SAN JACINTO DE YAGUACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092053', '0920', 'GRAL. PEDRO J. MONTERO (BOLICHE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092055', '0920', 'YAGUACHI VIEJO (CONE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092056', '0920', 'VIRGEN DE FATIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092150', '0921', 'GENERAL VILLAMIL (PLAYAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092250', '0922', 'SIMON BOLIVAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092251', '0922', 'CRNEL. LORENZO DE GARAICOA (PEDREGAL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092350', '0923', 'CORONEL MARCELINO MARIDUEÑA (SAN CARLOS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092450', '0924', 'LOMAS DE SARGENTILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092550', '0925', 'NARCISA DE JESUS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092750', '0927', 'GRAL. ANTONIO ELIZALDE (BUCAY)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('092850', '0928', 'ISIDRO AYORA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100150', '1001', 'SAN MIGUEL DE IBARRA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100151', '1001', 'AMBUQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100152', '1001', 'ANGOCHAGUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100153', '1001', 'CAROLINA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100154', '1001', 'LA ESPERANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100155', '1001', 'LITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100156', '1001', 'SALINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100157', '1001', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100250', '1002', 'ATUNTAQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100251', '1002', 'IMBAYA (SAN LUIS DE COBUENDO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100252', '1002', 'SAN FCO. DE NATABUELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100253', '1002', 'SAN JOSE DE CHALTURA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100254', '1002', 'SAN ROQUE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100350', '1003', 'COTACACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100351', '1003', 'APUELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100352', '1003', 'GARCIA MORENO (LLURIMAGUA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100353', '1003', 'IMANTAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100354', '1003', 'PEÑAHERRERA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100355', '1003', 'PLAZA GUTIERREZ (CALVARIO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100356', '1003', 'QUIROGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100357', '1003', '6 DE JULIO DE CUELLAJE (CAB. EN CUELLAJE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100358', '1003', 'VACAS GALINDO (EL CHURO) (CAB. EN SAN MIGUEL ALTO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100450', '1004', 'OTAVALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100451', '1004', 'DOCTOR MIGUEL EGAS CABEZAS (PEGUCHE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100452', '1004', 'EUGENIO ESPEJO (CALPAQUI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100453', '1004', 'GONZALEZ SUAREZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100454', '1004', 'PATAQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100455', '1004', 'SAN JOSE DE QUICHINCHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100456', '1004', 'SAN JUAN DE ILUMAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100457', '1004', 'SAN PABLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100458', '1004', 'SAN RAFAEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100459', '1004', 'SELVA ALEGRE (CAB. EN SAN MIGUEL DE PAMPLONA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100550', '1005', 'PIMAMPIRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100551', '1005', 'CHUGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100552', '1005', 'MARIANO ACOSTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100553', '1005', 'SAN  FCO. DE SIGSIPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100650', '1006', 'URCUQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100651', '1006', 'CAHUASQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100652', '1006', 'LA MERCED DE BUENOS AIRES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100653', '1006', 'PABLO ARENAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100654', '1006', 'SAN BLAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('100655', '1006', 'TUMBABIRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110150', '1101', 'LOJA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110151', '1101', 'CHANTACO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110152', '1101', 'CHUQUIRIBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110153', '1101', 'EL CISNE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110154', '1101', 'GUALEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110155', '1101', 'JIMBILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110156', '1101', 'MALACATOS (VALLADOLID)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110157', '1101', 'SAN LUCAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110158', '1101', 'SAN PEDRO DE VILCABAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110159', '1101', 'SANTIAGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110160', '1101', 'TAQUIL (MIGUEL RIOFRIO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110161', '1101', 'VILCABAMBA (VICTORIA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110162', '1101', 'YANGANA (ARSENIO CASTILLO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110163', '1101', 'QUINARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110250', '1102', 'CARIAMANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110251', '1102', 'COLAISACA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110252', '1102', 'EL LUCERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110253', '1102', 'UTUANA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110254', '1102', 'SANGUILLIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110350', '1103', 'CATAMAYO (LA TOMA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110351', '1103', 'EL TAMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110352', '1103', 'GUAYQUICHUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110353', '1103', 'SAN PEDRO DE LA BENDITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110354', '1103', 'ZAMBI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110450', '1104', 'CELICA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110451', '1104', 'CRUZPAMBA (CAB EN CARLOS BUSTAMANTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110455', '1104', 'POZUL (SAN JUAN DE POZUL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110456', '1104', 'SABANILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110457', '1104', 'TNTE. MAXIMILIANO RODRIGUEZ LOAIZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110550', '1105', 'CHAGUARPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110551', '1105', 'BUENAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110552', '1105', 'EL ROSARIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110553', '1105', 'SANTA RUFINA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110554', '1105', 'AMARILLOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110650', '1106', 'AMALUZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110651', '1106', 'BELLAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110652', '1106', 'JIMBURA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110653', '1106', 'SANTA TERESITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110654', '1106', '27 DE ABRIL (CAB. EN LA NARANJA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110655', '1106', 'EL INGENIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110656', '1106', 'EL AIRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110750', '1107', 'GONZANAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110751', '1107', 'CHANGAIMINA (LA LIBERTAD)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110753', '1107', 'NAMBACOLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110754', '1107', 'PURUNUMA (EGUIGUREN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110756', '1107', 'SACAPALCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110850', '1108', 'MACARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110851', '1108', 'LARAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110852', '1108', 'LA VICTORIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110853', '1108', 'SABIANGO (LA CAPILLA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110950', '1109', 'CATACOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110951', '1109', 'CANGONAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110952', '1109', 'GUACHANAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110954', '1109', 'LAURO GUERRERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110956', '1109', 'ORIANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110957', '1109', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110958', '1109', 'CASANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('110959', '1109', 'YAMANA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111050', '1110', 'ALAMOR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111051', '1110', 'CIANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111052', '1110', 'EL ARENAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111053', '1110', 'EL LIMO (MARIANA DE JESUS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111054', '1110', 'MERCADILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111055', '1110', 'VICENTINO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111150', '1111', 'SARAGURO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111151', '1111', 'EL PARAISO DE CELEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111152', '1111', 'EL TABLON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111153', '1111', 'LLUZHAPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111154', '1111', 'MANU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111155', '1111', 'SAN ANTONIO DE QUMBE (CUMBE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111156', '1111', 'SAN PABLO DE TENTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111157', '1111', 'SAN SEBASTIAN DE YULUC')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111158', '1111', 'SELVA ALEGRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111159', '1111', 'URDANETA (PAQUISHAPA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111160', '1111', 'SUMAYPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111250', '1112', 'SOZORANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111251', '1112', 'NUEVA FATIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111252', '1112', 'TACAMOROS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111350', '1113', 'ZAPOTILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111351', '1113', 'MANGAHURCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111352', '1113', 'GARZAREAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111353', '1113', 'LIMONES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111354', '1113', 'PALETILLAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111355', '1113', 'BOLASPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111356', '1113', 'CAZADEROS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111450', '1114', 'PINDAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111451', '1114', 'CHAQUINAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111452', '1114', '12 DE DICIEMBRE (CAB. EN ACHIOTES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111453', '1114', 'MILAGROS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111550', '1115', 'QUILANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111551', '1115', 'FUNDOCHAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111552', '1115', 'SAN ANTONIO DE LAS ARADAS (CAB. EN LAS ARADAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111650', '1116', 'OLMEDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('111651', '1116', 'LA TINGUE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120150', '1201', 'BABAHOYO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120152', '1201', 'CARACOL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120153', '1201', 'FEBRES CORDERO (LAS JUNTAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120154', '1201', 'PIMOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120155', '1201', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120250', '1202', 'BABA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120251', '1202', 'GUARE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120252', '1202', 'ISLA DE BEJUCAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120350', '1203', 'MONTALVO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120351', '1203', 'LA ESMERALDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120450', '1204', 'PUEBLOVIEJO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120451', '1204', 'PUERTO PECHICHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120452', '1204', 'SAN JUAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120550', '1205', 'QUEVEDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120553', '1205', 'SAN CARLOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120555', '1205', 'LA ESPERANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120650', '1206', 'CATARAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120651', '1206', 'RICAURTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120750', '1207', 'VENTANAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120752', '1207', 'ZAPOTAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120753', '1207', 'CHACARITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120754', '1207', 'LOS ANGELES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120850', '1208', 'VINCES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120851', '1208', 'ANTONIO SOTOMAYOR (CAB. EN PLAYAS DE VINCES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('120950', '1209', 'PALENQUE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('121050', '1210', 'SAN JACINTO DE BUENA FE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('121051', '1210', 'PATRICIA PILAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('121150', '1211', 'VALENCIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('121250', '1212', 'MOCACHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('121350', '1213', 'QUINSALOMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130150', '1301', 'PORTOVIEJO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130151', '1301', 'ABDON CALDERON (SAN FRANCISCO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130152', '1301', 'ALHAJUELA (BAJO GRANDE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130153', '1301', 'CRUCITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130154', '1301', 'PUEBLO NUEVO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130155', '1301', 'RIOCHICO (RIO CHICO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130156', '1301', 'SAN PLACIDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130157', '1301', 'CHIRIJOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130250', '1302', 'CALCETA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130251', '1302', 'MEMBRILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130252', '1302', 'QUIROGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130350', '1303', 'CHONE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130351', '1303', 'BOYACA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130352', '1303', 'CANUTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130353', '1303', 'CONVENTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130354', '1303', 'CHIBUNGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130355', '1303', 'ELOY ALFARO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130356', '1303', 'RICAURTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130357', '1303', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130450', '1304', 'EL CARMEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130451', '1304', 'WILFRIDO LOOR MOREIRA (MAICITO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130452', '1304', 'SAN PEDRO DE SUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130550', '1305', 'FLAVIO ALFARO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130551', '1305', 'SAN FRANCISCO DE NOVILLO (CAB. EN NOVILLO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130552', '1305', 'ZAPALLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130650', '1306', 'JIPIJAPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130651', '1306', 'AMERICA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130652', '1306', 'EL ANEGADO (CAB. EN ELOY ALFARO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130653', '1306', 'JULCUY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130654', '1306', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130656', '1306', 'MEMBRILLAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130657', '1306', 'PEDRO PABLO GOMEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130658', '1306', 'PUERTO DE CAYO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130750', '1307', 'JUNIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130850', '1308', 'MANTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130851', '1308', 'SAN LORENZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130852', '1308', 'SANTA MARIANITA (BOCA DE PACOCHE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130950', '1309', 'MONTECRISTI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('130952', '1309', 'LA PILA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131050', '1310', 'PAJAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131051', '1310', 'CAMPOZANO (LA PALMA DE PAJAN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131052', '1310', 'CASCOL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131053', '1310', 'GUALE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131054', '1310', 'LASCANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131150', '1311', 'PICHINCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131151', '1311', 'BARRAGANETE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131152', '1311', 'SAN SEBASTIAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131250', '1312', 'ROCAFUERTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131350', '1313', 'SANTA ANA DE VUELTA LARGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131351', '1313', 'AYACUCHO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131352', '1313', 'HONORATO VASQUEZ (CAB EN VASQUEZ)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131353', '1313', 'LA UNION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131355', '1313', 'SAN PABLO (CAB EN PUEBLO NUEVO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131450', '1314', 'BAHIA DE CARAQUEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131453', '1314', 'CHARAPOTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131457', '1314', 'SAN ISIDRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131550', '1315', 'TOSAGUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131551', '1315', 'BACHILLERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131552', '1315', 'ANGEL PEDRO GILER (LA ESTANCILLA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131650', '1316', 'SUCRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131651', '1316', 'BELLAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131652', '1316', 'NOBOA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131653', '1316', 'ARQ. SIXTO DURAN BALLEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131750', '1317', 'PEDERNALES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131751', '1317', 'COJIMIES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131752', '1317', '10 DE AGOSTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131753', '1317', 'ATAHUALPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131850', '1318', 'OLMEDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131950', '1319', 'PUERTO LOPEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131951', '1319', 'MACHALILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('131952', '1319', 'SALANGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('132050', '1320', 'JAMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('132150', '1321', 'JARAMIJO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('132250', '1322', 'SAN VICENTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('132251', '1322', 'CANOA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140150', '1401', 'MACAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140151', '1401', 'ALSHI (CAB EN 9 DE OCTUBRE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140153', '1401', 'GENERAL PROAÑO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140156', '1401', 'SAN ISIDRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140157', '1401', 'SEVILLA DON BOSCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140158', '1401', 'SINAI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140160', '1401', 'ZUÑA (ZUÑAC)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140162', '1401', 'CUCHAENTZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140164', '1401', 'RIO BLANCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140250', '1402', 'GUALAQUIZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140251', '1402', 'AMAZONAS (ROSARIO DE CUYES)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140252', '1402', 'BERMEJOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140253', '1402', 'BOMBOIZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140254', '1402', 'CHIGUINDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140255', '1402', 'EL ROSARIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140256', '1402', 'NUEVA TARQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140257', '1402', 'SAN MIGUEL DE CUYES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140258', '1402', 'EL IDEAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140350', '1403', 'GRAL. LEONIDAS PLAZA GUTIERREZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140351', '1403', 'INDANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140353', '1403', 'SAN ANTONIO (CAB EN SAN ANTONIO CENTRO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140356', '1403', 'SAN MIGUEL DE CONCHAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140357', '1403', 'STA SUSANA DE CHIVIAZA (CAB EN CHIVIAZA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140358', '1403', 'YUNGANZA (CAB EN EL ROSARIO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140450', '1404', 'PALORA (METZERA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140451', '1404', 'ARAPICOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140452', '1404', 'CUMANDA (CAB EN COLONIA AGRICOLA SEVILLA DEL ORO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140454', '1404', 'SANGAY (CAB EN NAYAMANACA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140455', '1404', '16 DE AGOSTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140550', '1405', 'SANTIAGO DE MENDEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140551', '1405', 'COPAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140552', '1405', 'CHUPIANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140553', '1405', 'PATUCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140554', '1405', 'SAN LUIS DE EL ACHO (CAB EN EL ACHO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140556', '1405', 'TAYUZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140557', '1405', 'SAN FRANCISCO DE CHINIMBIMI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140650', '1406', 'SUCUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140651', '1406', 'ASUNCION')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140652', '1406', 'HUAMBI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140655', '1406', 'SANTA MARIANITA DE JESUS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140750', '1407', 'HUAMBOYA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140751', '1407', 'CHIGUAZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140850', '1408', 'SAN JUAN BOSCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140851', '1408', 'PAN DE AZUCAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140852', '1408', 'SAN CARLOS DE LIMON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140853', '1408', 'SAN JACINTO DE WAKAMBEIS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140854', '1408', 'SANTIAGO DE PANANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140950', '1409', 'TAISHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140951', '1409', 'HUASAGA (CAB EN WAMPUIK)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140952', '1409', 'MACUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140953', '1409', 'TUUTINENTZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('140954', '1409', 'PUMPUENTSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141050', '1410', 'LOGROÑO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141051', '1410', 'YAUPI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141052', '1410', 'SHIMPIS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141150', '1411', 'PABLO SEXTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141250', '1412', 'SANTIAGO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('141251', '1412', 'SAN JOSE DE MORONA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150150', '1501', 'TENA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150151', '1501', 'AHUANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150153', '1501', 'CHONTAPUNTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150154', '1501', 'PANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150155', '1501', 'PUERTO MISAHUALLI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150156', '1501', 'PUERTO NAPO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150157', '1501', 'TALAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150158', '1501', 'SAN JUAN DE MUYUNA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150350', '1503', 'ARCHIDONA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150352', '1503', 'COTUNDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150354', '1503', 'SAN PABLO DE USHPAYACU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150356', '1503', 'HATUN SUMAKU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150450', '1504', 'EL CHACO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150451', '1504', 'GONZALO DIAZ DE PINEDA (EL BOMBON)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150452', '1504', 'LINARES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150453', '1504', 'OYACACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150454', '1504', 'SANTA ROSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150455', '1504', 'SARDINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150750', '1507', 'BAEZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150751', '1507', 'COSANGA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150752', '1507', 'CUYUJA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150753', '1507', 'PAPALLACTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150754', '1507', 'SAN FRANCISCO DE BORJA (VIRGILIO DAVILA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150756', '1507', 'SUMACO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('150950', '1509', 'CARLOS JULIO AROSEMENA TOLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160150', '1601', 'PUYO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160152', '1601', 'CANELOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160154', '1601', 'DIEZ DE AGOSTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160155', '1601', 'FATIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160156', '1601', 'MONTALVO (ANDOAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160157', '1601', 'POMONA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160158', '1601', 'RIO CORRIENTES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160159', '1601', 'RIO TIGRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160161', '1601', 'SARAYACU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160162', '1601', 'SIMON BOLIVAR (CAB. EN MUSHULLACTA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160163', '1601', 'TARQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160164', '1601', 'TENIENTE HUGO ORTIZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160165', '1601', 'VERACRUZ (INDILLAMA) (CAB. EN INDILLAMA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160166', '1601', 'EL TRIUNFO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160250', '1602', 'MERA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160251', '1602', 'MADRE TIERRA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160252', '1602', 'SHELL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160350', '1603', 'SANTA CLARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160351', '1603', 'SAN JOSE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160450', '1604', 'ARAJUNO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('160451', '1604', 'CURARAY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170150', '1701', 'QUITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170151', '1701', 'ALANGASI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170152', '1701', 'AMAGUAÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170153', '1701', 'ATAHUALPA (HABASPAMBA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170154', '1701', 'CALACALI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170155', '1701', 'CALDERON (CARAPUNGO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170156', '1701', 'CONOCOTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170157', '1701', 'CUMBAYA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170158', '1701', 'CHAVEZPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170159', '1701', 'CHECA (CHILPA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170160', '1701', 'EL QUINCHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170161', '1701', 'GUALEA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170162', '1701', 'GUANGOPOLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170163', '1701', 'GUAYLLABAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170164', '1701', 'LA MERCED')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170165', '1701', 'LLANO CHICO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170166', '1701', 'LLOA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170168', '1701', 'NANEGAL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170169', '1701', 'NANEGALITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170170', '1701', 'NAYON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170171', '1701', 'NONO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170172', '1701', 'PACTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170174', '1701', 'PERUCHO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170175', '1701', 'PIFO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170176', '1701', 'PINTAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170177', '1701', 'POMASQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170178', '1701', 'PUELLARO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170179', '1701', 'PUEMBO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170180', '1701', 'SAN ANTONIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170181', '1701', 'SAN JOSE DE MINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170183', '1701', 'TABABELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170184', '1701', 'TUMBACO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170185', '1701', 'YARUQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170186', '1701', 'ZAMBIZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170250', '1702', 'CAYAMBE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170251', '1702', 'ASCAZUBI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170252', '1702', 'CANGAHUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170253', '1702', 'OLMEDO (PESILLO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170254', '1702', 'OTON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170255', '1702', 'SANTA ROSA DE CUZUBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170256', '1702', 'SAN JOSE DE AYORA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170350', '1703', 'MACHACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170351', '1703', 'ALOAG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170352', '1703', 'ALOASI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170353', '1703', 'CUTUGLAHUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170354', '1703', 'EL CHAUPI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170355', '1703', 'MANUEL CORNEJO ASTORGA (TANDAPI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170356', '1703', 'TAMBILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170357', '1703', 'UYUMBICHO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170450', '1704', 'TABACUNDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170451', '1704', 'LA ESPERANZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170452', '1704', 'MALCHINGUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170453', '1704', 'TOCACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170454', '1704', 'TUPIGACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170550', '1705', 'SANGOLQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170551', '1705', 'COTOGCHOA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170552', '1705', 'RUMIPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170750', '1707', 'SAN MIGUEL DE LOS BANCOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170751', '1707', 'MINDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170850', '1708', 'PEDRO VICENTE MALDONADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('170950', '1709', 'PUERTO QUITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180150', '1801', 'AMBATO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180151', '1801', 'AMBATILLO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180152', '1801', 'ATAHUALPA (CHISALATA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180153', '1801', 'AUGUSTO N. MARTINEZ (MUNDUGLEO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180154', '1801', 'CONSTANTINO FERNANDEZ (CAB. EN CULLITAHUA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180155', '1801', 'HUACHI GRANDE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180156', '1801', 'IZAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180157', '1801', 'JUAN BENIGNO VELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180158', '1801', 'MONTALVO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180159', '1801', 'PASA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180160', '1801', 'PICAIGUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180161', '1801', 'PILAGUIN (PILAHUIN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180162', '1801', 'QUISAPINCHA (QUIZAPINCHA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180163', '1801', 'SAN BARTOLOME DE PINLLOG')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180164', '1801', 'SAN FERNANDO (PASA SAN FERNANDO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180165', '1801', 'SANTA ROSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180166', '1801', 'TOTORAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180167', '1801', 'CUNCHIBAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180168', '1801', 'UNAMUNCHO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180250', '1802', 'BAÑOS DE AGUA SANTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180251', '1802', 'LLIGUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180252', '1802', 'RIO NEGRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180253', '1802', 'RIO VERDE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180254', '1802', 'ULBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180350', '1803', 'CEVALLOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180450', '1804', 'MOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180451', '1804', 'PINGUILI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180550', '1805', 'PATATE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180551', '1805', 'EL TRIUNFO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180552', '1805', 'LOS ANDES (CAB. EN POATUG)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180553', '1805', 'SUCRE (CAB. EN SUCRE-PATATE URCU)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180650', '1806', 'QUERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180651', '1806', 'RUMIPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180652', '1806', 'YANAYACU - MOCHAPATA (CAB. EN YANAYACU)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180750', '1807', 'PELILEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180751', '1807', 'BENITEZ (PACHANLICA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180752', '1807', 'BOLIVAR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180753', '1807', 'COTALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180754', '1807', 'CHIQUICHA (CAB. EN CHIQUICHA GRANDE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180755', '1807', 'EL ROSARIO (RUMICHACA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180756', '1807', 'GARCIA MORENO (CHUMAQUI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180757', '1807', 'GUAMBALO (HUAMBALO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180758', '1807', 'SALASACA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180850', '1808', 'PILLARO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180851', '1808', 'BAQUERIZO MORENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180852', '1808', 'EMILIO MARIA TERAN (RUMIPAMBA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180853', '1808', 'MARCOS ESPINEL (CHACATA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180854', '1808', 'PRESIDENTE URBINA (CHAGRAPAMBA -PATZUCUL)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180855', '1808', 'SAN ANDRES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180856', '1808', 'SAN JOSE DE POALO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180857', '1808', 'SAN MIGUELITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180950', '1809', 'TISALEO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('180951', '1809', 'QUINCHICOTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190150', '1901', 'ZAMORA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190151', '1901', 'CUMBARATZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190152', '1901', 'GUADALUPE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190153', '1901', 'IMBANA (LA VICTORIA DE IMBANA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190155', '1901', 'SABANILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190156', '1901', 'TIMBARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190158', '1901', 'SAN CARLOS DE LAS MINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190250', '1902', 'ZUMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190251', '1902', 'CHITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190252', '1902', 'EL CHORRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190254', '1902', 'LA CHONTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190256', '1902', 'PUCAPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190259', '1902', 'SAN ANDRES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190350', '1903', 'GUAYZIMI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190351', '1903', 'ZURMI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190352', '1903', 'NUEVO PARAISO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190450', '1904', '28 DE MAYO (SAN JOSE DE YACUAMBI)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190451', '1904', 'LA PAZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190452', '1904', 'TUTUPALI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190550', '1905', 'YANTZAZA (YANZATZA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190551', '1905', 'CHICAÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190553', '1905', 'LOS ENCUENTROS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190650', '1906', 'EL PANGUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190651', '1906', 'EL GUISME')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190652', '1906', 'PACHICUTZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190653', '1906', 'TUNDAYME')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190750', '1907', 'ZUMBI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190752', '1907', 'TRIUNFO-DORADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190753', '1907', 'PANGUINTZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190850', '1908', 'PALANDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190851', '1908', 'EL PORVENIR DEL CARMEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190852', '1908', 'SAN FRANCISCO DEL VERGEL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190853', '1908', 'VALLADOLID')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190854', '1908', 'LA CANELA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190950', '1909', 'PAQUISHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190951', '1909', 'BELLAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('190952', '1909', 'NUEVO QUITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200150', '2001', 'PUERTO BAQUERIZO MORENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200151', '2001', 'EL PROGRESO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200152', '2001', 'ISLA SANTA MARÍA (FLOREANA) (CAB. EN  PTO. VELASCO IBARRA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200250', '2002', 'PUERTO VILLAMIL')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200251', '2002', 'TOMAS DE BERLANGA (SANTO TOMAS)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200350', '2003', 'PUERTO AYORA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200351', '2003', 'BELLAVISTA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('200352', '2003', 'SANTA ROSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210150', '2101', 'NUEVA LOJA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210152', '2101', 'DURENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210153', '2101', 'GENERAL FARFAN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210155', '2101', 'EL ENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210156', '2101', 'PACAYACU')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210157', '2101', 'JAMBELI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210158', '2101', 'SANTA CECILIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210160', '2101', '10 DE AGOSTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210250', '2102', 'LUMBAQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210251', '2102', 'EL REVENTADOR')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210252', '2102', 'GONZALO PIZARRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210254', '2102', 'PUERTO LIBRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210350', '2103', 'PUERTO EL CARMEN DEL PUTUMAYO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210351', '2103', 'PALMA ROJA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210352', '2103', 'PUERTO BOLIVAR (PUERTO MONTUFAR)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210353', '2103', 'PUERTO RODRIGUEZ')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210354', '2103', 'SANTA ELENA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210450', '2104', 'SHUSHUFINDI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210451', '2104', 'LIMONCOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210452', '2104', 'PAÑACOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210453', '2104', 'SAN ROQUE (CAB. EN SAN VICENTE)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210454', '2104', 'SAN PEDRO DE LOS COFANES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210455', '2104', 'SIETE DE JULIO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210550', '2105', 'LA BONITA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210551', '2105', 'EL PLAYON DE SAN FRANCISCO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210552', '2105', 'LA SOFIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210553', '2105', 'ROSA FLORIDA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210554', '2105', 'SANTA BARBARA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210650', '2106', 'EL DORADO DE CASCALES')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210651', '2106', 'SANTA ROSA DE SUCUMBIOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210652', '2106', 'SEVILLA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210750', '2107', 'TARAPOA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210751', '2107', 'CUYABENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('210752', '2107', 'AGUAS NEGRAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220150', '2201', 'PUERTO FRANCISCO DE ORELLANA (EL COCA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220151', '2201', 'DAYUMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220152', '2201', 'TARACOA (NUEVA ESPERANZA: YUCA)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220153', '2201', 'ALEJANDRO LABAKA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220154', '2201', 'EL DORADO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220155', '2201', 'EL EDEN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220156', '2201', 'GARCIA MORENO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220157', '2201', 'INES ARANGO (CAB. EN WESTERN)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220158', '2201', 'LA BELLEZA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220159', '2201', 'NUEVO PARAISO (CAB. EN UNION )')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220160', '2201', 'SAN JOSE DE GUAYUSA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220161', '2201', 'SAN LUIS DE ARMENIA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220250', '2202', 'NUEVO ROCAFUERTE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220251', '2202', 'CAPITAN AUGUSTO RIVADENEYRA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220252', '2202', 'CONONACO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220253', '2202', 'SANTA MARIA DE HUIRIRIMA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220254', '2202', 'TIPUTINI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220255', '2202', 'YASUNI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220350', '2203', 'LA JOYA DE LOS SACHAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220351', '2203', 'ENOKANQUI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220352', '2203', 'POMPEYA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220353', '2203', 'SAN CARLOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220354', '2203', 'SAN SEBASTIAN DEL COCA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220355', '2203', 'LAGO SAN PEDRO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220356', '2203', 'RUMIPAMBA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220357', '2203', 'TRES DE NOVIEMBRE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220358', '2203', 'UNION MILAGREÑA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220450', '2204', 'LORETO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220451', '2204', 'AVILA (CAB. EN HUIRUNO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220452', '2204', 'PUERTO MURIALDO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220453', '2204', 'SAN JOSE DE PAYAMINO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220454', '2204', 'SAN JOSE DE DAHUANO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('220455', '2204', 'SAN VICENTE DE HUATICOCHA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230150', '2301', 'SANTO DOMINGO DE LOS COLORADOS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230151', '2301', 'ALLURIQUIN')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230152', '2301', 'PUERTO LIMON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230153', '2301', 'LUZ DE AMERICA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230154', '2301', 'SAN JACINTO DEL BUA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230155', '2301', 'VALLE HERMOSO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230156', '2301', 'EL ESFUERZO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('230157', '2301', 'SANTA MARIA DEL TOACHI')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240150', '2401', 'SANTA ELENA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240151', '2401', 'ATAHUALPA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240152', '2401', 'COLONCHE')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240153', '2401', 'CHANDUY')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240154', '2401', 'MANGLARALTO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240155', '2401', 'SIMON BOLIVAR (JULIO MORENO)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240156', '2401', 'SAN JOSE DE ANCON')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240250', '2402', 'LA LIBERTAD')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240350', '2403', 'SALINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240351', '2403', 'ANCONCITO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('240352', '2403', 'JOSE LUIS TAMAYO (MUEY)')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('900151', '9001', 'LAS GOLONDRINAS')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('900351', '9003', 'MANGA DEL CURA')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
INSERT INTO geo_parishes (id, canton_id, name) VALUES ('900451', '9004', 'EL PIEDRERO')
ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;
