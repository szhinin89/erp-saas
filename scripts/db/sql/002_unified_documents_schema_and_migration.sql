-- =============================================================================
-- ERP SaaS — Unificación de documentos (VENTAS / COMPRAS / GASTOS)
-- =============================================================================
-- Objetivo: reemplazar el modelo duplicado (capa operativa + capa electrónica)
-- por tablas unificadas por dominio, con tablas electronic_doc 1:1 aisladas
-- y tablas de retención separadas.
--
-- PREREQUISITOS:
--   - Backup completo de la base antes de ejecutar.
--   - Ejecutar en ventana de mantenimiento (bloquea escrituras si se hace en prod).
--   - Revisar mapeo company_id → tenant_id vía tabla companies.
--
-- ORDEN DE EJECUCIÓN:
--   1) Sección A — Tabla auxiliar de mapeo
--   2) Sección B — CREATE TABLE (nuevo esquema)
--   3) Sección C — Migración de datos (INSERT … SELECT)
--   4) Sección D — Vistas de compatibilidad
--   5) Sección E — Validación (conteos)
--   6) Sección F — Deprecación (COMENTADA; ejecutar tras validar app)
-- =============================================================================

BEGIN;

-- =============================================================================
-- A) TABLA AUXILIAR DE MAPEO (legacy → nuevo UUID)
-- =============================================================================
CREATE TABLE IF NOT EXISTS _migration_id_map (
    domain          VARCHAR(20)  NOT NULL,  -- SALES, PURCHASE, EXPENSE, USER
    legacy_table    VARCHAR(64)  NOT NULL,
    legacy_id       UUID         NOT NULL,
    new_table       VARCHAR(64)  NOT NULL,
    new_id          UUID         NOT NULL,
    merged_into     UUID,                   -- si fue deduplicado, apunta al id ganador
    migrated_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (domain, legacy_table, legacy_id)
);

CREATE INDEX IF NOT EXISTS ix_migration_map_new
    ON _migration_id_map (new_table, new_id);

-- =============================================================================
-- B) NUEVO ESQUEMA — CREATE TABLE
-- =============================================================================

-- ---------------------------------------------------------------------------
-- B.1 VENTAS
-- ---------------------------------------------------------------------------
CREATE TABLE sales_document (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    company_id              UUID,                       -- desde capa electrónica / multi-empresa
    branch_id               UUID,                       -- facturas operativas
    customer_id             UUID,                       -- FK customers (nullable en proforma sin cliente)
    warehouse_id            UUID,
    salesperson_id          UUID,
    doc_type                VARCHAR(20)     NOT NULL,   -- INVOICE, CREDIT_NOTE, DEBIT_NOTE, PROFORMA
    doc_number              VARCHAR(30),                -- invoice_number / order_number legado
    estab_code              CHAR(3),
    em_point_code           CHAR(3),
    sequential              VARCHAR(9),
    access_key              CHAR(49),                   -- denormalizado para búsqueda rápida
    issue_date              DATE            NOT NULL,
    due_date                DATE,
    delivery_date           DATE,
    reference_document_id   UUID,                       -- nota → factura origen
    reason                  VARCHAR(300),
    subtotal                NUMERIC(18,4) NOT NULL DEFAULT 0,
    vat_total               NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_discount          NUMERIC(18,4) NOT NULL DEFAULT 0,
    total                   NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_notes_applied     NUMERIC(18,4) NOT NULL DEFAULT 0,
    payment_method_code     VARCHAR(5)      NOT NULL DEFAULT '01',
    payment_days            SMALLINT        NOT NULL DEFAULT 0,
    currency                CHAR(3)         NOT NULL DEFAULT 'USD',
    status                  VARCHAR(30)     NOT NULL,
    notes                   VARCHAR(1000),
    remittance_key          VARCHAR(49),
    guide_doc_num           VARCHAR(20),
    journal_entry_id        UUID,
    -- auditoría
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    -- trazabilidad migración
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_sales_document PRIMARY KEY (id),
    CONSTRAINT fk_sales_document_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_document_company
        FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_document_branch
        FOREIGN KEY (branch_id) REFERENCES branches(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_document_customer
        FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_document_warehouse
        FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_document_reference
        FOREIGN KEY (reference_document_id) REFERENCES sales_document(id) ON DELETE RESTRICT,
    CONSTRAINT ck_sales_document_doc_type
        CHECK (doc_type IN ('INVOICE','CREDIT_NOTE','DEBIT_NOTE','PROFORMA'))
);

CREATE UNIQUE INDEX uq_sales_document_tenant_seq
    ON sales_document (tenant_id, estab_code, em_point_code, sequential)
    WHERE estab_code IS NOT NULL AND sequential IS NOT NULL;

CREATE UNIQUE INDEX uq_sales_document_tenant_access_key
    ON sales_document (tenant_id, access_key)
    WHERE access_key IS NOT NULL;

CREATE INDEX ix_sales_document_tenant_date_status_type
    ON sales_document (tenant_id, issue_date, status, doc_type);

CREATE INDEX ix_sales_document_tenant_customer_date
    ON sales_document (tenant_id, customer_id, issue_date)
    WHERE customer_id IS NOT NULL;

CREATE TABLE sales_detail (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id           UUID            NOT NULL,
    sales_document_id   UUID            NOT NULL,
    product_id          UUID,
    product_code        VARCHAR(100)    NOT NULL DEFAULT '',
    description         VARCHAR(500)    NOT NULL,
    unit_code           VARCHAR(20),
    quantity            NUMERIC(18,4)   NOT NULL DEFAULT 0,
    unit_price          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    discount_pct        NUMERIC(6,2)    NOT NULL DEFAULT 0,
    discount_amount     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    vat_code            VARCHAR(5)      NOT NULL DEFAULT '0',
    vat_percentage      NUMERIC(6,2)    NOT NULL DEFAULT 0,
    ice_code            VARCHAR(10),
    ice_percentage      NUMERIC(8,4),
    subtotal            NUMERIC(18,4)   NOT NULL DEFAULT 0,
    vat_amount          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    ice_amount          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total               NUMERIC(18,4)   NOT NULL DEFAULT 0,
    sort_order          SMALLINT        NOT NULL DEFAULT 0,
    additional_detail   JSONB,
    created_at          TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ,
    created_by          UUID,
    updated_by          UUID,
    legacy_source_table VARCHAR(64),
    legacy_source_id    UUID,
    CONSTRAINT pk_sales_detail PRIMARY KEY (id),
    CONSTRAINT fk_sales_detail_document
        FOREIGN KEY (sales_document_id) REFERENCES sales_document(id) ON DELETE CASCADE,
    CONSTRAINT fk_sales_detail_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT
);

CREATE INDEX ix_sales_detail_document
    ON sales_detail (sales_document_id);

CREATE INDEX ix_sales_detail_tenant_document
    ON sales_detail (tenant_id, sales_document_id);

CREATE TABLE sales_payment (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id           UUID            NOT NULL,
    sales_document_id   UUID            NOT NULL,
    payment_method      VARCHAR(5)      NOT NULL,
    amount              NUMERIC(18,4),
    payment_term        INTEGER,
    bank                VARCHAR(100),
    account_number      VARCHAR(50),
    created_at          TIMESTAMPTZ,
    legacy_source_table VARCHAR(64),
    legacy_source_id    UUID,
    CONSTRAINT pk_sales_payment PRIMARY KEY (id),
    CONSTRAINT fk_sales_payment_document
        FOREIGN KEY (sales_document_id) REFERENCES sales_document(id) ON DELETE CASCADE
);

CREATE INDEX ix_sales_payment_document
    ON sales_payment (sales_document_id);

-- Tabla 1:1 SRI — solo documentos electrónicos emitidos
CREATE TABLE sales_electronic_doc (
    sales_document_id       UUID            NOT NULL,   -- PK = FK 1:1
    tenant_id               UUID            NOT NULL,
    company_id              UUID            NOT NULL,
    emission_point_id       UUID            NOT NULL,
    legacy_electronic_doc_id UUID,                     -- electronic_doc.id origen
    doc_type_code           CHAR(2)         NOT NULL, -- 01 factura, 04 NC, 05 ND, etc.
    access_key              CHAR(49),
    auth_number             VARCHAR(49),
    auth_date               TIMESTAMPTZ,
    xml_signed_path         VARCHAR(500),
    xml_auth_path           VARCHAR(500),
    error_code              VARCHAR(10),
    error_message           VARCHAR(1000),
    subtotal_vat0           NUMERIC(18,4) NOT NULL DEFAULT 0,
    subtotal_taxable        NUMERIC(18,4) NOT NULL DEFAULT 0,
    subtotal_exempt         NUMERIC(18,4) NOT NULL DEFAULT 0,
    subtotal_no_object      NUMERIC(18,4) NOT NULL DEFAULT 0,
    subtotal_no_vat_obj     NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_ice               NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_other_taxes       NUMERIC(18,4) NOT NULL DEFAULT 0,
    additional_info         JSONB,
    -- datos comprador SRI (desde sales_invoice)
    buyer_id_type           VARCHAR(5),
    buyer_id_number         VARCHAR(32),
    buyer_name              VARCHAR(200),
    buyer_address           VARCHAR(500),
    buyer_email             VARCHAR(120),
    buyer_phone             VARCHAR(40),
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    CONSTRAINT pk_sales_electronic_doc PRIMARY KEY (sales_document_id),
    CONSTRAINT fk_sales_electronic_doc_document
        FOREIGN KEY (sales_document_id) REFERENCES sales_document(id) ON DELETE CASCADE,
    CONSTRAINT fk_sales_electronic_doc_company
        FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_electronic_doc_emission_point
        FOREIGN KEY (emission_point_id) REFERENCES emission_points(id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX uq_sales_electronic_doc_access_key
    ON sales_electronic_doc (access_key)
    WHERE access_key IS NOT NULL;

CREATE INDEX ix_sales_electronic_doc_tenant_access_key
    ON sales_electronic_doc (tenant_id, access_key)
    WHERE access_key IS NOT NULL;

-- Retenciones emitidas a clientes (sales_retention + withholding_cert)
CREATE TABLE sales_withholding (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    company_id              UUID,
    customer_id             UUID,           -- nullable si solo issuer_ruc (received_withholding)
    direction               VARCHAR(10)     NOT NULL DEFAULT 'ISSUED', -- ISSUED | RECEIVED
    sales_document_id       UUID,           -- factura relacionada
    issuer_ruc              VARCHAR(13),
    issuer_name             VARCHAR(200),
    voucher_type            VARCHAR(30)     NOT NULL,
    access_key              VARCHAR(49)     NOT NULL,
    issue_date              DATE            NOT NULL,
    estab_code              CHAR(3),
    em_point_code           CHAR(3),
    sequential              VARCHAR(9),
    total_retained          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    status                  VARCHAR(30)     NOT NULL,
    xml_path                VARCHAR(500),
    xml_signed_path         VARCHAR(500),
    xml_auth_path           VARCHAR(500),
    auth_number             VARCHAR(64),
    auth_date               TIMESTAMPTZ,
    error_message           VARCHAR(500),
    journal_entry_id        UUID,
    legacy_electronic_doc_id UUID,
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_sales_withholding PRIMARY KEY (id),
    CONSTRAINT ck_sales_withholding_direction
        CHECK (direction IN ('ISSUED','RECEIVED')),
    CONSTRAINT fk_sales_withholding_customer
        FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_withholding_document
        FOREIGN KEY (sales_document_id) REFERENCES sales_document(id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX uq_sales_withholding_tenant_access_key
    ON sales_withholding (tenant_id, access_key);

CREATE TABLE sales_withholding_line (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    sales_withholding_id    UUID            NOT NULL,
    tax_type                VARCHAR(20)     NOT NULL,
    retention_code          VARCHAR(10)     NOT NULL,
    taxable_base            NUMERIC(18,4)   NOT NULL,
    retention_pct           NUMERIC(9,4)    NOT NULL,
    amount_retained         NUMERIC(18,4)   NOT NULL,
    created_at              TIMESTAMPTZ,
    CONSTRAINT pk_sales_withholding_line PRIMARY KEY (id),
    CONSTRAINT fk_sales_withholding_line_parent
        FOREIGN KEY (sales_withholding_id) REFERENCES sales_withholding(id) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------------
-- B.2 COMPRAS
-- ---------------------------------------------------------------------------
CREATE TABLE purchase_document (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    company_id              UUID,
    supplier_id             UUID,                       -- nullable en ORDER draft
    doc_type                VARCHAR(20)     NOT NULL,   -- INVOICE, DEBIT_NOTE, ORDER
    doc_number              VARCHAR(30)     NOT NULL,   -- invoice_number / order_number
    access_key              VARCHAR(49),
    issue_date              DATE            NOT NULL,
    required_date           DATE,                       -- órdenes de compra
    due_date                DATE,
    subtotal                NUMERIC(18,4) NOT NULL DEFAULT 0,
    vat_total               NUMERIC(18,4) NOT NULL DEFAULT 0,
    total                   NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_notes_applied     NUMERIC(18,4) NOT NULL DEFAULT 0,
    currency                CHAR(3)         NOT NULL DEFAULT 'USD',
    status                  VARCHAR(30)     NOT NULL,
    payment_terms           VARCHAR(50),
    notes                   VARCHAR(1000),
    delivery_address        VARCHAR(500),
    target_warehouse_id     UUID,
    reference_document_id   UUID,                       -- nota → factura compra
    reason                  VARCHAR(300),
    xml_path                VARCHAR(500),
    journal_entry_id        UUID,
    validated_by            UUID,
    validated_at            TIMESTAMPTZ,
    approved_by             UUID,
    approved_at             TIMESTAMPTZ,
    rejected_by             UUID,
    rejected_at             TIMESTAMPTZ,
    rejection_reason        VARCHAR(500),
    sent_at                 TIMESTAMPTZ,
    closed_at               TIMESTAMPTZ,
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_purchase_document PRIMARY KEY (id),
    CONSTRAINT ck_purchase_document_doc_type
        CHECK (doc_type IN ('INVOICE','CREDIT_NOTE','DEBIT_NOTE','ORDER')),
    CONSTRAINT fk_purchase_document_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT,
    CONSTRAINT fk_purchase_document_supplier
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT,
    CONSTRAINT fk_purchase_document_reference
        FOREIGN KEY (reference_document_id) REFERENCES purchase_document(id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX uq_purchase_document_tenant_supplier_number
    ON purchase_document (tenant_id, supplier_id, doc_number)
    WHERE supplier_id IS NOT NULL AND doc_type = 'INVOICE';

CREATE UNIQUE INDEX uq_purchase_document_tenant_order_number
    ON purchase_document (tenant_id, doc_number)
    WHERE doc_type = 'ORDER';

CREATE UNIQUE INDEX uq_purchase_document_tenant_access_key
    ON purchase_document (tenant_id, access_key)
    WHERE access_key IS NOT NULL;

CREATE INDEX ix_purchase_document_tenant_date_status_type
    ON purchase_document (tenant_id, issue_date, status, doc_type);

CREATE INDEX ix_purchase_document_tenant_supplier_date
    ON purchase_document (tenant_id, supplier_id, issue_date);

CREATE TABLE purchase_detail (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    purchase_document_id    UUID            NOT NULL,
    product_id              UUID,
    product_code            VARCHAR(100),
    description             VARCHAR(500)    NOT NULL,
    unit_code               VARCHAR(20),
    quantity                NUMERIC(18,4)   NOT NULL DEFAULT 0,
    qty_received            NUMERIC(18,4)   NOT NULL DEFAULT 0,  -- órdenes
    unit_price              NUMERIC(18,4)   NOT NULL DEFAULT 0,
    discount_pct            NUMERIC(6,2)    NOT NULL DEFAULT 0,
    vat_code                VARCHAR(5)      NOT NULL DEFAULT '0',
    vat_percentage          NUMERIC(6,2)    NOT NULL DEFAULT 0,
    subtotal                NUMERIC(18,4)   NOT NULL DEFAULT 0,
    vat_amount              NUMERIC(18,4)   NOT NULL DEFAULT 0,
    total                   NUMERIC(18,4)   NOT NULL DEFAULT 0,
    sort_order              SMALLINT        NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_purchase_detail PRIMARY KEY (id),
    CONSTRAINT fk_purchase_detail_document
        FOREIGN KEY (purchase_document_id) REFERENCES purchase_document(id) ON DELETE CASCADE
);

CREATE INDEX ix_purchase_detail_document
    ON purchase_detail (purchase_document_id);

CREATE TABLE purchase_payment (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    purchase_document_id    UUID            NOT NULL,
    payment_method          VARCHAR(5)      NOT NULL,
    amount                  NUMERIC(18,4),
    payment_term            INTEGER,
    bank                    VARCHAR(100),
    account_number          VARCHAR(50),
    created_at              TIMESTAMPTZ,
    CONSTRAINT pk_purchase_payment PRIMARY KEY (id),
    CONSTRAINT fk_purchase_payment_document
        FOREIGN KEY (purchase_document_id) REFERENCES purchase_document(id) ON DELETE CASCADE
);

CREATE TABLE purchase_electronic_doc (
    purchase_document_id    UUID            NOT NULL,
    tenant_id               UUID            NOT NULL,
    company_id              UUID            NOT NULL,
    legacy_electronic_doc_id UUID,
    doc_type_code           CHAR(2)         NOT NULL,
    access_key              VARCHAR(49),
    auth_number             VARCHAR(49),
    auth_date               TIMESTAMPTZ,
    xml_path                VARCHAR(500),
    xml_signed_path         VARCHAR(500),
    xml_auth_path           VARCHAR(500),
    supplier_tax_id         VARCHAR(20),
    supplier_name           VARCHAR(200),
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    CONSTRAINT pk_purchase_electronic_doc PRIMARY KEY (purchase_document_id),
    CONSTRAINT fk_purchase_electronic_doc_document
        FOREIGN KEY (purchase_document_id) REFERENCES purchase_document(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX uq_purchase_electronic_doc_access_key
    ON purchase_electronic_doc (access_key)
    WHERE access_key IS NOT NULL;

-- Retenciones recibidas / emitidas a proveedores
CREATE TABLE purchase_withholding (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    company_id              UUID,
    supplier_id             UUID            NOT NULL,
    purchase_document_id    UUID,
    direction               VARCHAR(10)     NOT NULL DEFAULT 'RECEIVED', -- RECEIVED | ISSUED
    voucher_type            VARCHAR(30)     NOT NULL,
    access_key              VARCHAR(49)     NOT NULL,
    issue_date              DATE            NOT NULL,
    estab_code              CHAR(3),
    em_point_code           CHAR(3),
    sequential              VARCHAR(9),
    total_retained          NUMERIC(18,4)   NOT NULL DEFAULT 0,
    status                  VARCHAR(30)     NOT NULL,
    xml_path                VARCHAR(500),
    xml_signed_path         VARCHAR(500),
    xml_auth_path           VARCHAR(500),
    auth_number             VARCHAR(64),
    auth_date               TIMESTAMPTZ,
    error_message           VARCHAR(500),
    journal_entry_id        UUID,
    legacy_electronic_doc_id UUID,
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_purchase_withholding PRIMARY KEY (id),
    CONSTRAINT ck_purchase_withholding_direction
        CHECK (direction IN ('RECEIVED','ISSUED')),
    CONSTRAINT fk_purchase_withholding_supplier
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT,
    CONSTRAINT fk_purchase_withholding_document
        FOREIGN KEY (purchase_document_id) REFERENCES purchase_document(id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX uq_purchase_withholding_tenant_access_key
    ON purchase_withholding (tenant_id, access_key);

CREATE TABLE purchase_withholding_line (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    purchase_withholding_id UUID            NOT NULL,
    tax_type                VARCHAR(20)     NOT NULL,
    retention_code          VARCHAR(10)     NOT NULL,
    taxable_base            NUMERIC(18,4)   NOT NULL,
    retention_pct           NUMERIC(9,4)    NOT NULL,
    amount_retained         NUMERIC(18,4)   NOT NULL,
    created_at              TIMESTAMPTZ,
    CONSTRAINT pk_purchase_withholding_line PRIMARY KEY (id),
    CONSTRAINT fk_purchase_withholding_line_parent
        FOREIGN KEY (purchase_withholding_id) REFERENCES purchase_withholding(id) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------------
-- B.3 GASTOS
-- ---------------------------------------------------------------------------
CREATE TABLE expense_document (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    supplier_id             UUID,
    doc_type                VARCHAR(20)     NOT NULL DEFAULT 'INVOICE',
    doc_number              VARCHAR(30),
    access_key              VARCHAR(49),
    issue_date              DATE            NOT NULL,
    concept                 VARCHAR(500)    NOT NULL,
    category                VARCHAR(50)     NOT NULL,   -- FK lógica a expense_category.code
    subtotal                NUMERIC(18,4) NOT NULL DEFAULT 0,
    tax_total               NUMERIC(18,4) NOT NULL DEFAULT 0,
    total                   NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_notes_applied     NUMERIC(18,4) NOT NULL DEFAULT 0,
    status                  VARCHAR(20)     NOT NULL,
    notes                   VARCHAR(1000),
    xml_path                VARCHAR(500),
    journal_entry_id        UUID,
    is_active               BOOLEAN         NOT NULL DEFAULT TRUE,
    validated_by            UUID,
    validated_at            TIMESTAMPTZ,
    approved_by             UUID,
    approved_at             TIMESTAMPTZ,
    rejected_by             UUID,
    rejected_at             TIMESTAMPTZ,
    rejection_reason        VARCHAR(500),
    created_at              TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    legacy_source_table     VARCHAR(64),
    legacy_source_id        UUID,
    CONSTRAINT pk_expense_document PRIMARY KEY (id),
    CONSTRAINT ck_expense_document_doc_type
        CHECK (doc_type IN ('INVOICE','RECEIPT','OTHER'))
);

CREATE UNIQUE INDEX uq_expense_document_tenant_access_key
    ON expense_document (tenant_id, access_key)
    WHERE access_key IS NOT NULL;

CREATE INDEX ix_expense_document_tenant_date_status_type
    ON expense_document (tenant_id, issue_date, status, doc_type);

CREATE INDEX ix_expense_document_tenant_category
    ON expense_document (tenant_id, category);

-- expense_detail: renombrar FK expense_id → expense_document_id (migración abajo)
CREATE TABLE expense_electronic_doc (
    expense_document_id     UUID            NOT NULL,
    tenant_id               UUID            NOT NULL,
    company_id              UUID,
    legacy_electronic_doc_id UUID,
    access_key              VARCHAR(49),
    auth_number             VARCHAR(49),
    auth_date               TIMESTAMPTZ,
    xml_path                VARCHAR(500),
    created_at              TIMESTAMPTZ,
    CONSTRAINT pk_expense_electronic_doc PRIMARY KEY (expense_document_id),
    CONSTRAINT fk_expense_electronic_doc_document
        FOREIGN KEY (expense_document_id) REFERENCES expense_document(id) ON DELETE CASCADE
);

CREATE TABLE expense_payment (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               UUID            NOT NULL,
    expense_document_id     UUID            NOT NULL,
    payment_method          VARCHAR(5)      NOT NULL DEFAULT '01',
    amount                  NUMERIC(18,4),
    created_at              TIMESTAMPTZ,
    CONSTRAINT pk_expense_payment PRIMARY KEY (id),
    CONSTRAINT fk_expense_payment_document
        FOREIGN KEY (expense_document_id) REFERENCES expense_document(id) ON DELETE CASCADE
);

-- =============================================================================
-- C) MIGRACIÓN DE DATOS
-- =============================================================================

-- ---------------------------------------------------------------------------
-- C.1 VENTAS — sales_bill → sales_document (capa operativa, prioridad 1)
-- ---------------------------------------------------------------------------
INSERT INTO sales_document (
    id, tenant_id, branch_id, customer_id, warehouse_id,
    doc_type, estab_code, em_point_code, sequential, access_key,
    issue_date, subtotal, vat_total, total_discount, total,
    payment_method_code, payment_days, status, notes,
    journal_entry_id, created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    b.id,
    b.tenant_id,
    b.branch_id,
    b.customer_id,
    b.warehouse_id,
    CASE
        WHEN b.doc_type IN ('04') THEN 'CREDIT_NOTE'
        WHEN b.doc_type IN ('05') THEN 'DEBIT_NOTE'
        WHEN b.doc_type IN ('PROFORMA','proforma') THEN 'PROFORMA'
        ELSE 'INVOICE'
    END,
    b.estab_code,
    b.em_point_code,
    b.sequential,
    NULLIF(TRIM(b.access_key), ''),
    b.issue_date,
    b.subtotal,
    b.vat_total,
    b.total_discount,
    b.total,
    b.payment_method_code,
    b.payment_days,
    b.status,
    b.notes,
    b.journal_entry_id,
    b.created_at,
    b.updated_at,
    b.created_by,
    b.updated_by,
    'sales_bill',
    b.id
FROM sales_bill b;

INSERT INTO _migration_id_map (domain, legacy_table, legacy_id, new_table, new_id)
SELECT 'SALES', 'sales_bill', id, 'sales_document', id
FROM sales_bill;

-- Líneas operativas
INSERT INTO sales_detail (
    id, tenant_id, sales_document_id, product_id, product_code,
    description, quantity, unit_price, discount_amount,
    vat_code, vat_percentage, subtotal, vat_amount, total,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    l.id, l.tenant_id, l.sales_bill_id, l.product_id, l.product_code,
    l.description, l.quantity, l.unit_price, l.discount_amount,
    l.vat_code, l.vat_percentage, l.subtotal, l.vat_total, l.total,
    l.created_at, l.updated_at, l.created_by, l.updated_by,
    'sales_bill_line', l.id
FROM sales_bill_line l
WHERE EXISTS (SELECT 1 FROM sales_document d WHERE d.id = l.sales_bill_id);

-- Pago único desde cabecera (cuando no hay doc_payment)
INSERT INTO sales_payment (tenant_id, sales_document_id, payment_method, amount, legacy_source_table)
SELECT tenant_id, id, payment_method_code, total, 'sales_bill'
FROM sales_document
WHERE legacy_source_table = 'sales_bill'
  AND NOT EXISTS (
      SELECT 1 FROM sales_payment p WHERE p.sales_document_id = sales_document.id
  );

-- ---------------------------------------------------------------------------
-- C.2 VENTAS — sales_note → sales_document (notas operativas)
-- ---------------------------------------------------------------------------
INSERT INTO sales_document (
    id, tenant_id, customer_id, warehouse_id,
    doc_type, estab_code, em_point_code, sequential, access_key,
    issue_date, subtotal, vat_total, total, reference_document_id,
    reason, status, journal_entry_id,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    n.id,
    n.tenant_id,
    b.customer_id,
    b.warehouse_id,
    CASE WHEN UPPER(n.note_type) IN ('CREDIT','CREDITO','04') THEN 'CREDIT_NOTE' ELSE 'DEBIT_NOTE' END,
    n.estab_code,
    n.em_point_code,
    n.sequential,
    n.access_key,
    n.issue_date,
    n.subtotal,
    n.vat_total,
    n.total,
    COALESCE(m.new_id, n.original_bill_id),
    n.reason,
    n.status,
    n.journal_entry_id,
    n.created_at,
    n.updated_at,
    n.created_by,
    n.updated_by,
    'sales_note',
    n.id
FROM sales_note n
JOIN sales_bill b ON b.id = n.original_bill_id
LEFT JOIN _migration_id_map m
    ON m.domain = 'SALES' AND m.legacy_table = 'sales_bill' AND m.legacy_id = n.original_bill_id
WHERE NOT EXISTS (SELECT 1 FROM sales_document d WHERE d.id = n.id);

INSERT INTO _migration_id_map (domain, legacy_table, legacy_id, new_table, new_id)
SELECT 'SALES', 'sales_note', n.id, 'sales_document', n.id
FROM sales_note n
WHERE NOT EXISTS (
    SELECT 1 FROM _migration_id_map m
    WHERE m.legacy_table = 'sales_note' AND m.legacy_id = n.id
);

INSERT INTO sales_detail (
    id, tenant_id, sales_document_id, product_id, product_code,
    description, quantity, unit_price, vat_code, vat_percentage,
    subtotal, vat_amount, total, legacy_source_table, legacy_source_id
)
SELECT
    l.id, l.tenant_id, l.sales_note_id, l.product_id, l.product_code,
    l.description, l.quantity, l.unit_price, l.vat_code, l.vat_percentage,
    l.subtotal, l.vat_total, l.total, 'sales_note_line', l.id
FROM sales_note_line l
WHERE EXISTS (SELECT 1 FROM sales_document d WHERE d.id = l.sales_note_id);

-- XML / auth de notas → sales_electronic_doc
INSERT INTO sales_electronic_doc (
    sales_document_id, tenant_id, company_id, emission_point_id,
    doc_type_code, access_key, auth_number, auth_date,
    xml_signed_path, xml_auth_path, error_message
)
SELECT
    d.id,
    d.tenant_id,
    c.id,
    ep.id,
    CASE d.doc_type WHEN 'CREDIT_NOTE' THEN '04' WHEN 'DEBIT_NOTE' THEN '05' ELSE '01' END,
    d.access_key,
    n.auth_number,
    n.auth_date,
    n.xml_signed_path,
    n.xml_auth_path,
    n.error_message
FROM sales_document d
JOIN sales_note n ON n.id = d.legacy_source_id AND d.legacy_source_table = 'sales_note'
JOIN companies c ON c.tenant_id = d.tenant_id
JOIN emission_points ep ON ep.company_id = c.id
    AND ep.establishment_code = d.estab_code
    AND ep.code = d.em_point_code
WHERE NOT EXISTS (SELECT 1 FROM sales_electronic_doc e WHERE e.sales_document_id = d.id)
ON CONFLICT (sales_document_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- C.3 VENTAS — capa electrónica (electronic_doc + sales_invoice)
-- Prioridad: si access_key ya existe en sales_document (desde sales_bill), ENRIQUECER;
-- si no existe, INSERT nuevo documento.
-- ---------------------------------------------------------------------------

-- 3a) Enriquecer documentos existentes por access_key
UPDATE sales_document d
SET
    company_id = ed.company_id,
    access_key = COALESCE(d.access_key, ed.access_key),
    total_discount = GREATEST(d.total_discount, COALESCE(ed.total_discount, 0)),
    updated_at = GREATEST(d.updated_at, ed.updated_at)
FROM electronic_doc ed
JOIN sales_invoice si ON si.id = ed.id
JOIN companies c ON c.id = ed.company_id
WHERE d.tenant_id = c.tenant_id
  AND d.access_key IS NOT NULL
  AND ed.access_key = d.access_key;

INSERT INTO sales_electronic_doc (
    sales_document_id, tenant_id, company_id, emission_point_id,
    legacy_electronic_doc_id, doc_type_code, access_key, auth_number, auth_date,
    xml_signed_path, xml_auth_path, error_code, error_message,
    subtotal_vat0, subtotal_taxable, subtotal_exempt, subtotal_no_object,
    subtotal_no_vat_obj, total_ice, total_other_taxes, additional_info,
    buyer_id_type, buyer_id_number, buyer_name, buyer_address, buyer_email, buyer_phone,
    created_at, updated_at
)
SELECT
    d.id,
    c.tenant_id,
    ed.company_id,
    ed.emission_point_id,
    ed.id,
    ed.doc_type_code,
    ed.access_key,
    ed.auth_number,
    ed.auth_date,
    ed.xml_signed_path,
    ed.xml_auth_path,
    ed.error_code,
    ed.error_message,
    ed.subtotal_vat0,
    ed.subtotal_taxable,
    ed.subtotal_exempt,
    ed.subtotal_no_object,
    ed.subtotal_no_vat_obj,
    ed.total_ice,
    ed.total_other_taxes,
    ed.additional_info,
    si.buyer_id_type,
    si.buyer_id_number,
    si.buyer_name,
    si.buyer_address,
    si.buyer_email,
    si.buyer_phone,
    ed.created_at,
    ed.updated_at
FROM electronic_doc ed
JOIN sales_invoice si ON si.id = ed.id
JOIN companies c ON c.id = ed.company_id
JOIN sales_document d ON d.tenant_id = c.tenant_id AND d.access_key = ed.access_key
WHERE ed.doc_type_code = '01'
ON CONFLICT (sales_document_id) DO UPDATE SET
    legacy_electronic_doc_id = EXCLUDED.legacy_electronic_doc_id,
    buyer_id_type = COALESCE(sales_electronic_doc.buyer_id_type, EXCLUDED.buyer_id_type),
    buyer_name = COALESCE(sales_electronic_doc.buyer_name, EXCLUDED.buyer_name);

-- 3b) Facturas electrónicas SIN par operativo → nuevo sales_document
INSERT INTO sales_document (
    id, tenant_id, company_id, customer_id, warehouse_id, salesperson_id,
    doc_type, estab_code, em_point_code, sequential, access_key,
    issue_date, delivery_date, subtotal, vat_total, total_discount, total,
    status, notes, remittance_key, guide_doc_num, journal_entry_id,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    ed.id,
    c.tenant_id,
    ed.company_id,
    si.customer_id,
    si.warehouse_id,
    si.salesperson_id,
    'INVOICE',
    ed.establishment_code,
    ed.emission_point_code,
    ed.sequential,
    ed.access_key,
    COALESCE(ed.issue_date::date, CURRENT_DATE),
    si.delivery_date,
    COALESCE(ed.subtotal_taxable + ed.subtotal_vat0 + ed.subtotal_exempt, 0),
    COALESCE(ed.total_vat, 0),
    COALESCE(ed.total_discount, 0),
    COALESCE(ed.total, 0),
    ed.status,
    ed.notes,
    si.remittance_key,
    si.guide_doc_num,
    ed.journal_entry_id,
    ed.created_at,
    ed.updated_at,
    ed.created_by,
    ed.updated_by,
    'electronic_doc',
    ed.id
FROM electronic_doc ed
JOIN sales_invoice si ON si.id = ed.id
JOIN companies c ON c.id = ed.company_id
WHERE ed.doc_type_code = '01'
  AND NOT EXISTS (
      SELECT 1 FROM sales_document d
      WHERE d.access_key = ed.access_key AND d.tenant_id = c.tenant_id
  )
  AND NOT EXISTS (SELECT 1 FROM sales_document d WHERE d.id = ed.id);

INSERT INTO _migration_id_map (domain, legacy_table, legacy_id, new_table, new_id)
SELECT 'SALES', 'electronic_doc', ed.id, 'sales_document', ed.id
FROM electronic_doc ed
JOIN sales_invoice si ON si.id = ed.id
WHERE NOT EXISTS (
    SELECT 1 FROM _migration_id_map m WHERE m.legacy_table = 'electronic_doc' AND m.legacy_id = ed.id
);

INSERT INTO sales_electronic_doc (
    sales_document_id, tenant_id, company_id, emission_point_id,
    legacy_electronic_doc_id, doc_type_code, access_key, auth_number, auth_date,
    xml_signed_path, xml_auth_path, error_code, error_message,
    subtotal_vat0, subtotal_taxable, subtotal_exempt, subtotal_no_object,
    subtotal_no_vat_obj, total_ice, total_other_taxes, additional_info,
    buyer_id_type, buyer_id_number, buyer_name, buyer_address, buyer_email, buyer_phone
)
SELECT
    ed.id, c.tenant_id, ed.company_id, ed.emission_point_id,
    ed.id, ed.doc_type_code, ed.access_key, ed.auth_number, ed.auth_date,
    ed.xml_signed_path, ed.xml_auth_path, ed.error_code, ed.error_message,
    ed.subtotal_vat0, ed.subtotal_taxable, ed.subtotal_exempt, ed.subtotal_no_object,
    ed.subtotal_no_vat_obj, ed.total_ice, ed.total_other_taxes, ed.additional_info,
    si.buyer_id_type, si.buyer_id_number, si.buyer_name, si.buyer_address, si.buyer_email, si.buyer_phone
FROM electronic_doc ed
JOIN sales_invoice si ON si.id = ed.id
JOIN companies c ON c.id = ed.company_id
WHERE NOT EXISTS (SELECT 1 FROM sales_electronic_doc x WHERE x.sales_document_id = ed.id)
ON CONFLICT DO NOTHING;

-- Detalle electrónico (solo si el documento no tiene líneas operativas)
INSERT INTO sales_detail (
    tenant_id, sales_document_id, product_id, product_code, description,
    unit_code, quantity, unit_price, discount_pct, discount_amount,
    vat_code, vat_percentage, ice_code, ice_percentage,
    subtotal, vat_amount, ice_amount, total, sort_order, additional_detail,
    legacy_source_table, legacy_source_id
)
SELECT
    c.tenant_id,
    m.new_id,
    idet.product_id,
    COALESCE(idet.product_code, ''),
    idet.description,
    idet.unit_code,
    idet.qty,
    idet.unit_price,
    idet.discount_pct,
    idet.discount_amount,
    idet.vat_code,
    idet.vat_percentage,
    idet.ice_code,
    idet.ice_percentage,
    idet.subtotal,
    idet.vat_amount,
    idet.ice_amount,
    idet.total,
    idet.sort_order,
    idet.additional_detail,
    'invoice_detail',
    idet.id
FROM invoice_detail idet
JOIN _migration_id_map m ON m.legacy_table = 'electronic_doc' AND m.legacy_id = idet.doc_id
JOIN companies c ON c.id = (SELECT company_id FROM sales_document WHERE id = m.new_id)
WHERE NOT EXISTS (
    SELECT 1 FROM sales_detail sd WHERE sd.sales_document_id = m.new_id
);

-- Pagos electrónicos
INSERT INTO sales_payment (
    tenant_id, sales_document_id, payment_method, amount, payment_term, bank, account_number,
    legacy_source_table, legacy_source_id
)
SELECT
    c.tenant_id,
    m.new_id,
    dp.payment_method,
    dp.amount,
    dp.payment_term,
    dp.bank,
    dp.account_number,
    'doc_payment',
    dp.id
FROM doc_payment dp
JOIN _migration_id_map m ON m.legacy_table = 'electronic_doc' AND m.legacy_id = dp.doc_id
JOIN sales_document sd ON sd.id = m.new_id
JOIN companies c ON c.tenant_id = sd.tenant_id;

-- ---------------------------------------------------------------------------
-- C.4 VENTAS — credit_note / debit_note electrónicas
-- ---------------------------------------------------------------------------
INSERT INTO sales_document (
    id, tenant_id, company_id, doc_type,
    estab_code, em_point_code, sequential, access_key,
    issue_date, subtotal, vat_total, total, status, notes,
    reference_document_id, reason, journal_entry_id,
    created_at, updated_at, legacy_source_table, legacy_source_id
)
SELECT
    ed.id,
    c.tenant_id,
    ed.company_id,
    CASE WHEN cn.id IS NOT NULL THEN 'CREDIT_NOTE' ELSE 'DEBIT_NOTE' END,
    ed.establishment_code,
    ed.emission_point_code,
    ed.sequential,
    ed.access_key,
    COALESCE(ed.issue_date::date, CURRENT_DATE),
    COALESCE(ed.subtotal_taxable + ed.subtotal_vat0, 0),
    COALESCE(ed.total_vat, 0),
    COALESCE(ed.total, 0),
    ed.status,
    ed.notes,
    COALESCE(
        (SELECT new_id FROM _migration_id_map
         WHERE legacy_table = 'electronic_doc'
           AND legacy_id = COALESCE(cn.modified_doc_id, dn.modified_doc_id)),
        cn.modified_doc_id,
        dn.modified_doc_id
    ),
    COALESCE(cn.reason, dn.reason),
    ed.journal_entry_id,
    ed.created_at,
    ed.updated_at,
    'electronic_doc',
    ed.id
FROM electronic_doc ed
JOIN companies c ON c.id = ed.company_id
LEFT JOIN credit_note cn ON cn.id = ed.id
LEFT JOIN debit_note dn ON dn.id = ed.id
WHERE ed.doc_type_code IN ('04','05')
  AND NOT EXISTS (SELECT 1 FROM sales_document d WHERE d.id = ed.id);

INSERT INTO sales_detail (
    tenant_id, sales_document_id, product_id, product_code, description,
    unit_code, quantity, unit_price, discount_pct, vat_code, vat_percentage,
    subtotal, vat_amount, total, sort_order, legacy_source_table, legacy_source_id
)
SELECT
    sd.tenant_id,
    sd.id,
    nd.product_id,
    nd.product_code,
    nd.description,
    nd.unit_code,
    nd.qty,
    nd.unit_price,
    nd.discount_pct,
    nd.vat_code,
    nd.vat_percentage,
    nd.subtotal,
    nd.vat_amount,
    nd.total,
    nd.sort_order,
    'note_detail',
    nd.id
FROM note_detail nd
JOIN sales_document sd ON sd.legacy_source_id = nd.doc_id AND sd.legacy_source_table = 'electronic_doc';

-- ---------------------------------------------------------------------------
-- C.5 VENTAS — retenciones (sales_retention + withholding_cert)
-- ---------------------------------------------------------------------------
INSERT INTO sales_withholding (
    id, tenant_id, customer_id, direction, sales_document_id,
    voucher_type, access_key, issue_date, total_retained, status,
    xml_path, journal_entry_id, created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    r.id, r.tenant_id, r.customer_id, 'RECEIVED',
    COALESCE(m.new_id, r.sales_bill_id),
    r.voucher_type, r.access_key, r.issue_date, r.total_retained, r.status,
    r.xml_path, r.journal_entry_id,
    r.created_at, r.updated_at, r.created_by, r.updated_by,
    'sales_retention', r.id
FROM sales_retention r
LEFT JOIN _migration_id_map m
    ON m.legacy_table = 'sales_bill' AND m.legacy_id = r.sales_bill_id;

INSERT INTO sales_withholding_line (
    id, tenant_id, sales_withholding_id, tax_type, retention_code,
    taxable_base, retention_pct, amount_retained, created_at
)
SELECT
    l.id, l.tenant_id, l.sales_retention_id,
    l.tax_type, l.retention_code, l.taxable_base, l.retention_pct, l.amount_retained, l.created_at
FROM sales_retention_line l
WHERE EXISTS (SELECT 1 FROM sales_withholding w WHERE w.id = l.sales_retention_id);

-- ---------------------------------------------------------------------------
-- C.6 COMPRAS — purch_bill → purchase_document
-- ---------------------------------------------------------------------------
INSERT INTO purchase_document (
    id, tenant_id, supplier_id, doc_type, doc_number, access_key,
    issue_date, due_date, subtotal, vat_total, total, total_notes_applied,
    status, payment_terms, notes, journal_entry_id,
    validated_by, validated_at, approved_by, approved_at,
    rejected_by, rejected_at, rejection_reason,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    p.id, p.tenant_id, p.supplier_id, 'INVOICE', p.invoice_number, p.access_key,
    p.invoice_date, p.due_date, p.subtotal, p.vat_total, p.total, p.total_notes_applied,
    p.status::text, p.payment_terms, p.notes, p.journal_entry_id,
    p.validated_by, p.validated_at, p.approved_by, p.approved_at,
    p.rejected_by, p.rejected_at, p.rejection_reason,
    p.created_at, p.updated_at, p.created_by, p.updated_by,
    'purch_bill', p.id
FROM purch_bill p;

INSERT INTO _migration_id_map (domain, legacy_table, legacy_id, new_table, new_id)
SELECT 'PURCHASE', 'purch_bill', id, 'purchase_document', id FROM purch_bill;

INSERT INTO purchase_detail (
    id, tenant_id, purchase_document_id, product_id, description,
    quantity, unit_price, vat_code, subtotal, vat_amount, total,
    legacy_source_table, legacy_source_id
)
SELECT
    l.id, l.tenant_id, l.purch_bill_id, l.product_id, l.description,
    l.quantity, l.unit_price, l.vat_code, l.subtotal, l.vat_total, l.total,
    'purch_bill_line', l.id
FROM purch_bill_line l;

-- purchase_order
INSERT INTO purchase_document (
    id, tenant_id, supplier_id, doc_type, doc_number,
    issue_date, required_date, subtotal, vat_total, total, currency, status,
    notes, delivery_address, target_warehouse_id,
    sent_at, approved_at, approved_by, closed_at,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    o.id, o.tenant_id, o.supplier_id, 'ORDER', o.order_number,
    o.issue_date, o.required_date, o.subtotal, o.tax_total, o.total, o.currency, o.status,
    o.notes, o.delivery_address, o.target_warehouse_id,
    o.sent_at, o.approved_at, o.approved_by, o.closed_at,
    o.created_at, o.updated_at, o.created_by, o.updated_by,
    'purchase_order', o.id
FROM purchase_order o
WHERE NOT EXISTS (SELECT 1 FROM purchase_document d WHERE d.id = o.id);

INSERT INTO purchase_detail (
    tenant_id, purchase_document_id, product_id, description,
    quantity, qty_received, unit_price, subtotal, vat_amount, total,
    legacy_source_table, legacy_source_id
)
SELECT
    l.tenant_id, l.purchase_order_id, l.product_id, l.description,
    l.ordered_qty, l.invoiced_qty, l.unit_cost, l.subtotal, l.tax_amount, l.total,
    'purchase_order_line', l.id
FROM purchase_order_line l
WHERE EXISTS (SELECT 1 FROM purchase_document d WHERE d.id = l.purchase_order_id);

-- purch_note / supplier_note → purchase_document (notas)
INSERT INTO purchase_document (
    id, tenant_id, supplier_id, doc_type, doc_number, access_key,
    issue_date, subtotal, vat_total, total, status, reason,
    reference_document_id, xml_path, auth_number, auth_date, journal_entry_id,
    created_at, updated_at, legacy_source_table, legacy_source_id
)
SELECT
    n.id, n.tenant_id, n.supplier_id,
    CASE WHEN UPPER(n.note_type) IN ('CREDIT','CREDITO') THEN 'CREDIT_NOTE' ELSE 'DEBIT_NOTE' END,
    n.estab_code || '-' || n.em_point_code || '-' || n.sequential,
    n.access_key,
    n.issue_date, n.subtotal, n.vat_total, n.total, n.status, n.reason,
    COALESCE(m.new_id, n.purch_bill_id),
    n.xml_path, n.auth_number, n.auth_date, n.journal_entry_id,
    n.created_at, n.updated_at, 'purch_note', n.id
FROM purch_note n
LEFT JOIN _migration_id_map m ON m.legacy_table = 'purch_bill' AND m.legacy_id = n.purch_bill_id
WHERE NOT EXISTS (SELECT 1 FROM purchase_document d WHERE d.id = n.id);

-- purchase_invoice electrónica (sin duplicar access_key)
INSERT INTO purchase_document (
    id, tenant_id, company_id, supplier_id, doc_type, doc_number, access_key,
    issue_date, due_date, subtotal, vat_total, total, total_notes_applied,
    status, payment_terms, notes, journal_entry_id,
    validated_by, validated_at, approved_by, approved_at,
    rejected_by, rejected_at, rejection_reason,
    created_at, updated_at, created_by,
    legacy_source_table, legacy_source_id
)
SELECT
    pi.id,
    c.tenant_id,
    pi.company_id,
    pi.supplier_id,
    'INVOICE',
    pi.invoice_number,
    pi.access_key,
    COALESCE(pi.invoice_date::date, CURRENT_DATE),
    pi.due_date,
    pi.subtotal,
    pi.vat_total,
    pi.total,
    pi.notes_applied,
    pi.status,
    pi.payment_terms,
    pi.notes,
    pi.journal_entry_id,
    pi.validated_by, pi.validated_at, pi.approved_by, pi.approved_at,
    pi.rejected_by, pi.rejected_at, pi.rejection_reason,
    pi.created_at, pi.updated_at, pi.created_by,
    'purchase_invoice',
    pi.id
FROM purchase_invoice pi
JOIN companies c ON c.id = pi.company_id
WHERE pi.access_key IS NULL
   OR NOT EXISTS (
       SELECT 1 FROM purchase_document pd
       WHERE pd.tenant_id = c.tenant_id AND pd.access_key = pi.access_key
   );

INSERT INTO purchase_electronic_doc (
    purchase_document_id, tenant_id, company_id, legacy_electronic_doc_id,
    doc_type_code, access_key, xml_path
)
SELECT
    pd.id, pd.tenant_id, pi.company_id, pi.id, '01', pi.access_key, pi.xml_path
FROM purchase_invoice pi
JOIN companies c ON c.id = pi.company_id
JOIN purchase_document pd ON pd.legacy_source_id = pi.id AND pd.legacy_source_table = 'purchase_invoice'
ON CONFLICT DO NOTHING;

INSERT INTO purchase_detail (
    tenant_id, purchase_document_id, product_id, description,
    quantity, unit_price, discount_pct, subtotal, vat_code, vat_amount, total, sort_order,
    legacy_source_table, legacy_source_id
)
SELECT
    pd.tenant_id, pid.invoice_id, pid.product_id, pid.description,
    pid.qty, pid.unit_price, pid.discount_pct, pid.subtotal, pid.vat_code, pid.vat_amount, pid.total, pid.sort_order,
    'purch_inv_detail', pid.id
FROM purch_inv_detail pid
JOIN purchase_document pd ON pd.id = pid.invoice_id
WHERE NOT EXISTS (
    SELECT 1 FROM purchase_detail x WHERE x.purchase_document_id = pid.invoice_id
);

-- issued_retention → purchase_withholding (ISSUED)
INSERT INTO purchase_withholding (
    id, tenant_id, supplier_id, purchase_document_id, direction,
    voucher_type, access_key, issue_date, estab_code, em_point_code, sequential,
    total_retained, status, xml_signed_path, xml_auth_path, auth_number, auth_date,
    error_message, journal_entry_id, created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    r.id, r.tenant_id, r.supplier_id,
    COALESCE(m.new_id, r.purch_bill_id),
    'ISSUED',
    r.voucher_type, r.access_key, r.issue_date,
    r.establishment_code, r.emission_point_code, r.sequential,
    r.total_retained, r.status, r.xml_signed_path, r.xml_auth_path,
    r.auth_number, r.auth_date, r.error_message, r.journal_entry_id,
    r.created_at, r.updated_at, r.created_by, r.updated_by,
    'issued_retention', r.id
FROM issued_retention r
LEFT JOIN _migration_id_map m ON m.legacy_table = 'purch_bill' AND m.legacy_id = r.purch_bill_id;

-- received_withholding → sales_withholding (RECEIVED: cliente retiene sobre venta)
INSERT INTO sales_withholding (
    id, tenant_id, company_id, customer_id, direction, sales_document_id,
    issuer_ruc, issuer_name, voucher_type, access_key, issue_date,
    total_retained, status, xml_path, journal_entry_id,
    created_at, updated_at, created_by, legacy_source_table, legacy_source_id
)
SELECT
    rw.id,
    c.tenant_id,
    rw.company_id,
    rw.customer_id,
    'RECEIVED',
    COALESCE(m.new_id, rw.sales_doc_id),
    rw.issuer_ruc,
    rw.issuer_name,
    'RECEIVED',
    rw.access_key,
    COALESCE(rw.issue_date::date, CURRENT_DATE),
    rw.total_retained,
    rw.status,
    rw.xml_path,
    rw.journal_entry_id,
    rw.created_at,
    rw.updated_at,
    rw.created_by,
    'received_withholding',
    rw.id
FROM received_withholding rw
JOIN companies c ON c.id = rw.company_id
LEFT JOIN _migration_id_map m
    ON m.domain = 'SALES' AND m.legacy_table = 'electronic_doc' AND m.legacy_id = rw.sales_doc_id
WHERE NOT EXISTS (SELECT 1 FROM sales_withholding w WHERE w.id = rw.id);

-- purch_retention_line → purchase_withholding_line (líneas de issued_retention)
INSERT INTO purchase_withholding_line (
    id, tenant_id, purchase_withholding_id, tax_type, retention_code,
    taxable_base, retention_pct, amount_retained, created_at
)
SELECT
    l.id, l.tenant_id, l.issued_retention_id,
    l.tax_type, l.retention_code, l.taxable_base, l.retention_pct, l.amount_retained, l.created_at
FROM purch_retention_line l
WHERE EXISTS (SELECT 1 FROM purchase_withholding w WHERE w.id = l.issued_retention_id);

-- withholding_cert (retención emitida a proveedor) → purchase_withholding
INSERT INTO purchase_withholding (
    id, tenant_id, company_id, supplier_id, purchase_document_id, direction,
    voucher_type, access_key, issue_date, total_retained, status,
    legacy_electronic_doc_id, legacy_source_table, legacy_source_id
)
SELECT
    ed.id,
    c.tenant_id,
    ed.company_id,
    wc.supplier_id,
    wc.purchase_inv_id,
    'ISSUED',
    'WITHHOLDING',
    ed.access_key,
    COALESCE(ed.issue_date::date, CURRENT_DATE),
    COALESCE(wc.total_retained, ed.total),
    ed.status,
    ed.id,
    'withholding_cert',
    wc.id
FROM electronic_doc ed
JOIN withholding_cert wc ON wc.id = ed.id
JOIN companies c ON c.id = ed.company_id
WHERE NOT EXISTS (SELECT 1 FROM purchase_withholding w WHERE w.id = ed.id);

-- supplier_note → purchase_document (capa electrónica recibida)
INSERT INTO purchase_document (
    id, tenant_id, company_id, supplier_id, doc_type, doc_number, access_key,
    issue_date, subtotal, vat_total, total, status, reason,
    reference_document_id, journal_entry_id, created_at, created_by,
    legacy_source_table, legacy_source_id
)
SELECT
    sn.id,
    c.tenant_id,
    sn.company_id,
    sn.supplier_id,
    CASE WHEN sn.doc_type IN ('04') THEN 'DEBIT_NOTE' ELSE 'DEBIT_NOTE' END,
    sn.note_number,
    sn.access_key,
    COALESCE(sn.note_date::date, CURRENT_DATE),
    sn.subtotal,
    sn.vat_amount,
    sn.total,
    sn.status,
    sn.reason,
    COALESCE(m.new_id, sn.invoice_id),
    sn.journal_entry_id,
    sn.created_at,
    sn.created_by,
    'supplier_note',
    sn.id
FROM supplier_note sn
JOIN companies c ON c.id = sn.company_id
LEFT JOIN _migration_id_map m
    ON m.legacy_table IN ('purch_bill','purchase_invoice') AND m.legacy_id = sn.invoice_id
WHERE NOT EXISTS (SELECT 1 FROM purchase_document d WHERE d.id = sn.id);

-- purch_note_line → purchase_detail (notas operativas purch_note)
INSERT INTO purchase_detail (
    id, tenant_id, purchase_document_id, product_id, description,
    quantity, unit_price, subtotal, vat_amount, total,
    legacy_source_table, legacy_source_id
)
SELECT
    l.id, l.tenant_id, l.purch_note_id, l.product_id, l.description,
    l.quantity, l.unit_price, l.subtotal, l.vat_total, l.total,
    'purch_note_line', l.id
FROM purch_note_line l
WHERE EXISTS (SELECT 1 FROM purchase_document d WHERE d.id = l.purch_note_id)
  AND NOT EXISTS (SELECT 1 FROM purchase_detail pd WHERE pd.id = l.id);

-- ---------------------------------------------------------------------------
-- C.7 GASTOS — expense_invoice → expense_document
-- ---------------------------------------------------------------------------
INSERT INTO expense_document (
    id, tenant_id, supplier_id, doc_type, doc_number, access_key,
    issue_date, concept, category, subtotal, tax_total, total, total_notes_applied,
    status, notes, xml_path, journal_entry_id, is_active,
    validated_by, validated_at, approved_by, approved_at,
    rejected_by, rejected_at, rejection_reason,
    created_at, updated_at, created_by, updated_by,
    legacy_source_table, legacy_source_id
)
SELECT
    e.id, e.tenant_id, e.supplier_id,
    CASE
        WHEN UPPER(e.category) IN ('RECEIPT','RECIBO') THEN 'RECEIPT'
        WHEN UPPER(e.category) IN ('OTHER','OTRO') THEN 'OTHER'
        ELSE 'INVOICE'
    END,
    e.invoice_number, e.access_key,
    e.issue_date, e.concept, e.category, e.subtotal, e.tax_total, e.total, e.total_notes_applied,
    e.status::text, e.notes, e.xml_path, e.journal_entry_id, e.is_active,
    e.validated_by, e.validated_at, e.approved_by, e.approved_at,
    e.rejected_by, e.rejected_at, e.rejection_reason,
    e.created_at, e.updated_at, e.created_by, e.updated_by,
    'expense_invoice', e.id
FROM expense_invoice e;

-- Re-apuntar expense_detail (columna expense_id → expense_document_id)
-- Ejecutar solo si expense_detail sigue existiendo con expense_id:
-- ALTER TABLE expense_detail RENAME COLUMN expense_id TO expense_document_id;

-- ---------------------------------------------------------------------------
-- C.8 USUARIOS — users → identity_users (sin duplicar email)
-- ---------------------------------------------------------------------------
INSERT INTO identity_users (
    id, first_name, last_name, email, password_hash, is_active,
    created_at, updated_at, created_by, updated_by
)
SELECT
    u.id, u.first_name, u.last_name, u.email, u.password_hash, u.is_active,
    u.created_at, u.updated_at, u.created_by, u.updated_by
FROM users u
WHERE NOT EXISTS (
    SELECT 1 FROM identity_users i WHERE LOWER(i.email) = LOWER(u.email)
);

INSERT INTO _migration_id_map (domain, legacy_table, legacy_id, new_table, new_id)
SELECT
    'USER', 'users', u.id, 'identity_users',
    COALESCE(
        (SELECT i.id FROM identity_users i WHERE LOWER(i.email) = LOWER(u.email) LIMIT 1),
        u.id
    )
FROM users u;

-- memberships.user_id y otras FKs: actualizar en script aparte tras mapeo
-- UPDATE memberships m SET user_id = map.new_id
-- FROM _migration_id_map map
-- WHERE map.legacy_table = 'users' AND map.legacy_id = m.user_id AND map.merged_into IS NOT NULL;

COMMIT;

-- =============================================================================
-- D) VISTAS DE COMPATIBILIDAD (lectura legacy; no escribir)
-- =============================================================================

CREATE OR REPLACE VIEW sales_bill_v AS
SELECT
    d.id,
    d.tenant_id,
    d.branch_id,
    d.customer_id,
    d.warehouse_id,
    CASE d.doc_type
        WHEN 'CREDIT_NOTE' THEN '04'
        WHEN 'DEBIT_NOTE' THEN '05'
        WHEN 'PROFORMA' THEN 'PROFORMA'
        ELSE '01'
    END AS doc_type,
    d.estab_code,
    d.em_point_code,
    d.sequential,
    COALESCE(d.access_key, e.access_key) AS access_key,
    d.issue_date,
    d.subtotal,
    d.vat_total,
    d.total_discount,
    d.total,
    d.payment_method_code,
    d.payment_days,
    d.notes,
    d.status,
    e.xml_signed_path,
    e.xml_auth_path,
    e.auth_number,
    e.auth_date,
    e.error_message,
    d.journal_entry_id,
    d.created_at,
    d.updated_at,
    d.created_by,
    d.updated_by
FROM sales_document d
LEFT JOIN sales_electronic_doc e ON e.sales_document_id = d.id
WHERE d.doc_type IN ('INVOICE','PROFORMA');

CREATE OR REPLACE VIEW sales_note_v AS
SELECT
    d.id,
    d.tenant_id,
    d.reference_document_id AS original_bill_id,
    CASE d.doc_type WHEN 'CREDIT_NOTE' THEN 'CREDIT' ELSE 'DEBIT' END AS note_type,
    d.reason,
    '04' AS doc_type,
    d.estab_code,
    d.em_point_code,
    d.sequential,
    d.access_key,
    d.issue_date,
    d.subtotal,
    d.vat_total,
    d.total,
    d.status,
    e.xml_signed_path,
    e.xml_auth_path,
    e.auth_number,
    e.auth_date,
    e.error_message,
    d.journal_entry_id,
    d.created_at,
    d.updated_at,
    d.created_by,
    d.updated_by
FROM sales_document d
LEFT JOIN sales_electronic_doc e ON e.sales_document_id = d.id
WHERE d.doc_type IN ('CREDIT_NOTE','DEBIT_NOTE');

CREATE OR REPLACE VIEW purch_bill_v AS
SELECT
    d.id,
    d.tenant_id,
    d.supplier_id,
    d.doc_number AS invoice_number,
    d.access_key,
    d.xml_path,
    d.issue_date AS invoice_date,
    d.due_date,
    d.status,
    d.payment_terms,
    d.subtotal,
    d.vat_total,
    d.total,
    d.total_notes_applied,
    d.notes,
    d.validated_by,
    d.validated_at,
    d.approved_by,
    d.approved_at,
    d.rejected_by,
    d.rejected_at,
    d.rejection_reason,
    d.journal_entry_id,
    d.created_at,
    d.updated_at,
    d.created_by,
    d.updated_by
FROM purchase_document d
WHERE d.doc_type = 'INVOICE';

CREATE OR REPLACE VIEW expense_invoice_v AS
SELECT * FROM expense_document;

-- =============================================================================
-- E) VALIDACIÓN POST-MIGRACIÓN (ejecutar manualmente)
-- =============================================================================
-- SELECT 'sales_bill' src, COUNT(*) FROM sales_bill
-- UNION ALL SELECT 'sales_document INVOICE', COUNT(*) FROM sales_document WHERE doc_type='INVOICE';
--
-- SELECT legacy_table, COUNT(*) FROM _migration_id_map GROUP BY 1 ORDER BY 1;
--
-- SELECT access_key, COUNT(*) FROM sales_document WHERE access_key IS NOT NULL
-- GROUP BY access_key HAVING COUNT(*) > 1;

-- =============================================================================
-- F) TABLAS LEGACY A DEPRECAR / ELIMINAR (tras validar aplicación + EF)
-- =============================================================================
-- NO ejecutar hasta completar refactor de código y pruebas E2E.
--
-- VENTAS (operativa + electrónica unificada):
--   sales_bill, sales_bill_line
--   sales_note, sales_note_line
--   sales_invoice, credit_note, debit_note
--   invoice_detail, note_detail
--   electronic_doc (solo filas migradas; conservar si hay otros doc_type)
--   doc_payment
--
-- VENTAS retenciones:
--   sales_retention, sales_retention_line
--   withholding_cert (extensión de electronic_doc)
--
-- COMPRAS:
--   purch_bill, purch_bill_line
--   purch_note, purch_note_line
--   purchase_invoice, purch_inv_detail
--   purchase_order, purchase_order_line
--   supplier_note (si existe y fue migrada)
--   issued_retention, issued_retention_line (si existe)
--   received_withholding, purch_retention_line
--
-- GASTOS:
--   expense_invoice  (vista expense_invoice_v sustituye lectura)
--
-- USUARIOS:
--   users  (tras actualizar FKs: memberships, audit, etc.)
--
-- EJEMPLO DROP (comentado):
-- DROP VIEW IF EXISTS sales_bill_v CASCADE;
-- DROP TABLE IF EXISTS sales_bill_line CASCADE;
-- DROP TABLE IF EXISTS sales_bill CASCADE;
-- ... repetir en orden inverso de FKs ...

-- =============================================================================
-- G) ÍNDICES ADICIONALES RECOMENDADOS (resumen)
-- =============================================================================
-- Ya creados arriba:
--   ix_*_tenant_date_status_type  → reportes por rango fecha + status + doc_type
--   uq_*_access_key / ix_*_electronic_doc access_key → búsqueda SRI
--   ix_sales_document_tenant_customer_date → ventas por cliente
--
-- Opcional según volumen:
-- CREATE INDEX ix_sales_detail_product ON sales_detail (product_id) WHERE product_id IS NOT NULL;
-- CREATE INDEX ix_purchase_detail_product ON purchase_detail (product_id) WHERE product_id IS NOT NULL;
-- CREATE INDEX ix_expense_document_supplier ON expense_document (tenant_id, supplier_id);
