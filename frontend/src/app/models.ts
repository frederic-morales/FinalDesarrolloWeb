export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }
export interface User { id: string; userName: string; role: 'Administrador' | 'Operador'; isActive: boolean }
export interface LoginResponse { token: string; expiresAtUtc: string; user: User }
export interface Supplier { id: number; name: string; nit: string | null; contactName: string | null; email: string | null; phone: string | null; address: string | null; isActive: boolean }
export type SupplierInput = Omit<Supplier, 'id' | 'isActive'>;
export interface PaymentMethod { id: number; name: string; isActive: boolean; requiresCheckDetails: boolean }
export interface InvoiceLine { id?: number; description: string; quantity: number; unitPrice: number; subtotal?: number }
export interface Invoice { id: number; supplierId: number; supplierName: string; series: string; number: string; issueDate: string; dueDate: string; total: number; paid: number; debit: number; credit: number; balance: number; status: string; isOverdue?: boolean; lines?: InvoiceLine[] }
export interface InvoiceInput { supplierId: number; series: string; number: string; issueDate: string; dueDate: string; lines: Pick<InvoiceLine, 'description' | 'quantity' | 'unitPrice'>[] }
export interface Allocation { invoiceId: number; amount: number }
export interface CheckDetail { bankName: string; checkNumber: string; clearedOn: string | null }
export interface Payment { id: number; supplierId: number; supplierName: string; paymentMethodName: string; date: string; reference: string | null; amount: number; status: string; allocations?: (Allocation & { invoiceNumber: string })[]; check?: CheckDetail | null }
export interface PaymentInput { supplierId: number; paymentMethodId: number; date: string; reference: string; amount: number; allocations: Allocation[]; check: CheckDetail | null }
export type AdjustmentKind = 'CreditNote' | 'DebitNote' | 'OtherCredit' | 'OtherCharge';
export interface AdjustmentInput { supplierId: number; kind: AdjustmentKind; series: string; number: string; date: string; description: string; amount: number; allocations: Allocation[] }
export interface Adjustment { id: number; supplierId: number; supplierName: string; kind: AdjustmentKind; series: string; number: string; date: string; description?: string; amount: number; status: string; allocations?: (Allocation & { invoiceNumber: string })[] }
export interface Dashboard { activeSuppliers: number; pendingInvoices: number; overdueInvoices: number; totalOutstanding: number; totalOverdue: number }
export interface StatementMovement { date: string; type: string; documentId: number; description: string; charge: number; credit: number; runningBalance: number }
export interface Statement { supplierId: number; supplierName: string; from: string; to: string; openingBalance: number; closingBalance: number; movements: StatementMovement[] }
export interface SupplierBalance { supplierId: number; supplierName: string; asOf: string; pendingInvoices: number; outstanding: number; overdue: number }
