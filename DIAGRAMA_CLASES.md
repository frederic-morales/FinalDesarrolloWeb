# Diagrama de clases — Sistema de Cuentas por Pagar

Este diagrama representa las **entidades implementadas** en el backend. Los nombres y las relaciones corresponden a `Models/` y a `Contexts/PayablesDbContext.cs`. Los saldos se calculan a partir de documentos y aplicaciones; no son campos editables de estas clases.

```mermaid
classDiagram
    direction LR

    class Supplier {
        +int Id
        +string Nit
        +string Name
        +string ContactName
        +string Email
        +string Phone
        +string Address
        +bool IsActive
        +DateTime CreatedAtUtc
    }

    class PurchaseInvoice {
        +int Id
        +int SupplierId
        +string Series
        +string Number
        +DateOnly IssueDate
        +DateOnly DueDate
        +decimal Total
        +DocumentStatus Status
        +DateOnly CancelledOn
        +DateTime CreatedAtUtc
    }

    class InvoiceLine {
        +int Id
        +int PurchaseInvoiceId
        +string Description
        +decimal Quantity
        +decimal UnitPrice
        +decimal Subtotal
    }

    class Payment {
        +int Id
        +int SupplierId
        +int PaymentMethodId
        +Guid RecordedByUserId
        +DateOnly Date
        +string Reference
        +decimal Amount
        +DocumentStatus Status
        +DateOnly CancelledOn
        +DateTime CreatedAtUtc
    }

    class PaymentAllocation {
        +int Id
        +int PaymentId
        +int PurchaseInvoiceId
        +decimal Amount
    }

    class PaymentMethod {
        +int Id
        +string Name
        +bool IsActive
        +bool RequiresCheckDetails
    }

    class CheckDetail {
        +int PaymentId
        +string BankName
        +string CheckNumber
        +DateOnly ClearedOn
    }

    class SupplierAdjustment {
        +int Id
        +int SupplierId
        +Guid RecordedByUserId
        +AdjustmentKind Kind
        +string Series
        +string Number
        +DateOnly Date
        +string Description
        +decimal Amount
        +DocumentStatus Status
        +DateOnly CancelledOn
    }

    class AdjustmentAllocation {
        +int Id
        +int SupplierAdjustmentId
        +int PurchaseInvoiceId
        +decimal Amount
    }

    class AdjustmentKind {
        <<enumeration>>
        CreditNote
        DebitNote
        OtherCredit
        OtherCharge
    }

    class AppUser {
        +Guid Id
        +string UserName
        +string PasswordHash
        +Guid SecurityStamp
        +string Role
        +bool IsActive
        +DateTime CreatedAtUtc
    }

    class DocumentStatus {
        <<enumeration>>
        Active
        Cancelled
    }

    Supplier "1" --> "0..*" PurchaseInvoice : facturas
    PurchaseInvoice "1" *-- "0..*" InvoiceLine : lineas
    Supplier "1" --> "0..*" Payment : pagos
    PaymentMethod "1" --> "0..*" Payment : metodo
    AppUser "1" --> "0..*" Payment : registra
    Payment "1" --> "0..*" PaymentAllocation : aplicaciones
    PurchaseInvoice "1" --> "0..*" PaymentAllocation : recibe
    Payment "1" *-- "0..1" CheckDetail : cheque
    Supplier "1" --> "0..*" SupplierAdjustment : ajustes
    AppUser "1" --> "0..*" SupplierAdjustment : registra
    SupplierAdjustment "1" --> "0..*" AdjustmentAllocation : aplicaciones
    PurchaseInvoice "1" --> "0..*" AdjustmentAllocation : recibe
    PurchaseInvoice ..> DocumentStatus : estado
    Payment ..> DocumentStatus : estado
    SupplierAdjustment ..> DocumentStatus : estado
    SupplierAdjustment ..> AdjustmentKind : tipo
```

## Cómo leer la relación entre pagos y facturas

`PaymentAllocation` es la **entidad intermedia** entre `Payment` y `PurchaseInvoice`: un pago se puede distribuir entre varias facturas y una factura puede recibir varios pagos. `AdjustmentAllocation` cumple el mismo papel para las notas de crédito, notas de débito y otros ajustes. Cada aplicación guarda su importe. Las reglas de la API exigen al menos una línea por factura y una aplicación por pago o ajuste, aunque las colecciones aparecen como `0..*` porque esa es la cardinalidad estructural de las relaciones de EF Core.

`PaymentMethod` incluye el valor inicial `Cheque` y marca si el método exige `CheckDetail` (banco, número y fecha de cobro opcional). `SupplierAdjustment.Kind` distingue notas de crédito, notas de débito, otros abonos y otros cargos. Una nota se aplica por completo a facturas del mismo proveedor. El estado de cuenta es una respuesta calculada con facturas, pagos y ajustes; no existe una tabla que duplique esos movimientos.

Una factura activa tiene saldo `Total + Débitos - Créditos - Pagos`. `CancelledOn` permite registrar el contramovimiento en la fecha de anulación y conservar las consultas de cortes anteriores. Los documentos anulados antes de esta migración carecen de fecha histórica de anulación y no se pueden reconstruir retrospectivamente.
