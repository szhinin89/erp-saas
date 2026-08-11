# Manual de Usuario — ERP ZH Technologies

> Fuente única oficial para construir manuales, guías, capacitaciones y ayuda del sistema.

---

# 1. Conceptos base del ERP

## 1.1 Ítem, unidad base, presentación y código proveedor

El ítem representa el producto real.

La unidad base representa cómo se controla el inventario.

La presentación representa cómo se compra o vende.

El código proveedor representa cómo el proveedor lo llama en el XML.

Regla principal:

- No crear un ítem diferente por cada presentación.
- Crear un solo ítem por producto real.
- Configurar las presentaciones dentro del ítem.
- Asociar el código del proveedor con la presentación correcta.

Ejemplo de producto por unidad:

Ítem:
FANTA HARMONY NRJ 1350 PET

Unidad base:
UNIDAD

Presentaciones:
- UNIDAD X1
- PACA X12

Código proveedor:
3172 → PACA X12

Resultado:
2 PACA X12 = 24 UNIDADES.

Ejemplo de producto por peso:

Ítem:
ARROZ FLOR

Unidad base:
LIBRA

Presentaciones:
- LIBRA X1
- PESADA X5
- ARROBA X25
- QUINTAL X100

Resultado:
2 QUINTALES = 200 LIBRAS.

---

# 2. Inventario e ítems

Pendiente completar.

---

# 3. Compras

Pendiente completar.

---

# 4. Ventas

Pendiente completar.

---

# 5. Caja

Pendiente completar.

---

# 6. Procesos frecuentes

Pendiente completar.
