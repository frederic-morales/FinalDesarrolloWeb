import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { ApiService, apiError } from './api.service';
import { AuthService, isUnauthorized } from './auth.service';
import { exportPdf } from './report-pdf';
import { Adjustment, AdjustmentInput, Allocation, Dashboard, Invoice, InvoiceInput, Payment, PaymentInput, PaymentMethod, Statement, Supplier, SupplierBalance, SupplierInput, User } from './models';

function localDate(date = new Date()): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
const blankSupplier = (): SupplierInput => ({ name: '', nit: null, contactName: null, email: null, phone: null, address: null });
const blankInvoice = (): InvoiceInput => ({ supplierId: 0, series: '', number: '', issueDate: localDate(), dueDate: localDate(), lines: [{ description: '', quantity: 1, unitPrice: 0 }] });
const blankPayment = (): PaymentInput => ({ supplierId: 0, paymentMethodId: 0, date: localDate(), reference: '', amount: 0, allocations: [], check: null });
const blankAdjustment = (): AdjustmentInput => ({ supplierId: 0, kind: 'CreditNote', series: '', number: '', date: localDate(), description: '', amount: 0, allocations: [] });

@Component({
  selector: 'app-workspace',
  imports: [FormsModule, RouterLink, RouterLinkActive],
  templateUrl: './workspace.component.html'
})
export class WorkspaceComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly nav = [
    { key: 'resumen', label: 'Resumen', icon: '◫' },
    { key: 'proveedores', label: 'Proveedores', icon: '▤' },
    { key: 'facturas', label: 'Facturas', icon: '▧' },
    { key: 'pagos', label: 'Pagos', icon: '↗' },
    { key: 'movimientos', label: 'Notas y ajustes', icon: '⇄' },
    { key: 'reportes', label: 'Reportes', icon: '▥' },
    { key: 'configuracion', label: 'Configuración', icon: '⚙' }
  ];
  section = 'resumen';
  busy = false;
  error = '';
  notice = '';
  dashboard: Dashboard | null = null;
  suppliers: Supplier[] = [];
  invoices: Invoice[] = [];
  payments: Payment[] = [];
  adjustments: Adjustment[] = [];
  methods: PaymentMethod[] = [];
  users: User[] = [];
  payableRows: Invoice[] = [];
  supplierBalances: SupplierBalance[] = [];
  statement: Statement | null = null;
  selectedPayment: Payment | null = null;
  supplierSearch = '';
  supplierForm = blankSupplier();
  editingSupplierId: number | null = null;
  showSupplierForm = false;
  invoiceForm = blankInvoice();
  editingInvoiceId: number | null = null;
  showInvoiceForm = false;
  paymentForm = blankPayment();
  adjustmentForm = blankAdjustment();
  allocations: Record<number, number> = {};
  adjustmentAllocations: Record<number, number> = {};
  showPaymentForm = false;
  showAdjustmentForm = false;
  selectedAdjustment: Adjustment | null = null;
  methodName = '';
  methodRequiresCheckDetails = false;
  editingMethodId: number | null = null;
  userName = '';
  userPassword = '';
  userRole: User['role'] = 'Operador';
  resetUserId = '';
  resetPassword = '';
  reportSupplierId = 0;
  reportAsOf = localDate();
  reportOverdueOnly = false;
  statementFrom = `${localDate().slice(0, 8)}01`;
  statementTo = localDate();
  readonly money = (value: number | null | undefined) => new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(value ?? 0);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const section = params.get('section') ?? 'resumen';
      if (!this.nav.some(item => item.key === section)) { void this.router.navigateByUrl('/resumen'); return; }
      this.section = section;
      this.error = '';
      this.notice = '';
      void this.load();
    });
  }

  async load(): Promise<void> {
    await this.run(async () => {
      if (this.section === 'resumen') {
        [this.dashboard, this.payableRows] = await Promise.all([
          this.api.get<Dashboard>('/reports/dashboard'),
          this.api.all<Invoice>('/reports/payables', { asOf: localDate() })
        ]);
      }
      if (this.section === 'proveedores') this.suppliers = await this.api.all<Supplier>('/suppliers');
      if (this.section === 'facturas') {
        [this.suppliers, this.invoices] = await Promise.all([
          this.api.all<Supplier>('/suppliers'), this.api.all<Invoice>('/invoices')
        ]);
      }
      if (this.section === 'pagos') {
        [this.suppliers, this.invoices, this.payments, this.methods] = await Promise.all([
          this.api.all<Supplier>('/suppliers'), this.api.all<Invoice>('/invoices'),
          this.api.all<Payment>('/payments'), this.api.get<PaymentMethod[]>('/payment-methods')
        ]);
      }
      if (this.section === 'movimientos') {
        [this.suppliers, this.invoices, this.adjustments] = await Promise.all([
          this.api.all<Supplier>('/suppliers'), this.api.all<Invoice>('/invoices'),
          this.api.all<Adjustment>('/adjustments')
        ]);
      }
      if (this.section === 'reportes') {
        this.suppliers = await this.api.all<Supplier>('/suppliers');
        await this.loadPayables();
      }
      if (this.section === 'configuracion') {
        this.methods = await this.api.get<PaymentMethod[]>('/payment-methods');
        if (this.auth.isAdmin()) this.users = await this.api.get<User[]>('/users');
      }
    });
  }

  private async run(action: () => Promise<void>, success?: string): Promise<void> {
    this.error = '';
    this.notice = '';
    this.busy = true;
    try { await action(); if (success) this.notice = success; }
    catch (error) {
      this.error = apiError(error);
      if (isUnauthorized(error)) this.auth.logout();
    } finally { this.busy = false; }
  }

  get visibleSuppliers(): Supplier[] {
    const term = this.supplierSearch.trim().toLocaleLowerCase();
    return this.suppliers.filter(s => !term || `${s.name} ${s.nit ?? ''}`.toLocaleLowerCase().includes(term));
  }
  get invoiceTotal(): number { return this.invoiceForm.lines.reduce((sum, line) => sum + Number(line.quantity || 0) * Number(line.unitPrice || 0), 0); }
  get paymentTotal(): number { return Object.values(this.allocations).reduce((sum, value) => sum + Number(value || 0), 0); }
  get adjustmentTotal(): number { return Object.values(this.adjustmentAllocations).reduce((sum, value) => sum + Number(value || 0), 0); }
  get selectedMethod(): PaymentMethod | undefined { return this.methods.find(x => x.id === Number(this.paymentForm.paymentMethodId)); }
  get isCreditAdjustment(): boolean { return this.adjustmentForm.kind === 'CreditNote' || this.adjustmentForm.kind === 'OtherCredit'; }
  get adjustmentInvoices(): Invoice[] {
    return this.invoices.filter(x => x.supplierId === Number(this.adjustmentForm.supplierId) &&
      x.status === 'Active' && x.issueDate <= this.adjustmentForm.date &&
      (!this.isCreditAdjustment || x.balance > 0));
  }
  adjustmentLabel(kind: Adjustment['kind']): string {
    return ({ CreditNote: 'Nota de crédito', DebitNote: 'Nota de débito',
      OtherCredit: 'Otro abono', OtherCharge: 'Otro cargo' })[kind];
  }
  get payableInvoices(): Invoice[] {
    return this.invoices.filter(x => x.supplierId === Number(this.paymentForm.supplierId) && x.status === 'Active' && x.balance > 0 && x.issueDate <= this.paymentForm.date);
  }
  get topPayables(): Invoice[] { return this.payableRows.slice(0, 5); }
  get payableTotal(): number { return this.payableRows.reduce((sum, invoice) => sum + invoice.balance, 0); }

  newSupplier(): void { this.supplierForm = blankSupplier(); this.editingSupplierId = null; this.showSupplierForm = true; }
  editSupplier(s: Supplier): void {
    this.supplierForm = { name: s.name, nit: s.nit, contactName: s.contactName, email: s.email, phone: s.phone, address: s.address };
    this.editingSupplierId = s.id; this.showSupplierForm = true;
  }
  async saveSupplier(): Promise<void> {
    if (!this.supplierForm.name.trim()) { this.error = 'El nombre del proveedor es obligatorio.'; return; }
    await this.run(async () => {
      if (this.editingSupplierId) await this.api.put(`/suppliers/${this.editingSupplierId}`, this.supplierForm);
      else await this.api.post('/suppliers', this.supplierForm);
      this.showSupplierForm = false;
      this.suppliers = await this.api.all<Supplier>('/suppliers');
    }, 'Proveedor guardado.');
  }
  async toggleSupplier(s: Supplier): Promise<void> {
    await this.run(async () => {
      await this.api.put(`/suppliers/${s.id}/status`, { isActive: !s.isActive });
      this.suppliers = await this.api.all<Supplier>('/suppliers');
    }, 'Estado actualizado.');
  }

  newInvoice(): void { this.invoiceForm = blankInvoice(); this.editingInvoiceId = null; this.showInvoiceForm = true; }
  async editInvoice(i: Invoice): Promise<void> {
    await this.run(async () => {
      const full = await this.api.get<Invoice>(`/invoices/${i.id}`);
      this.invoiceForm = { supplierId: full.supplierId, series: full.series, number: full.number,
        issueDate: full.issueDate, dueDate: full.dueDate, lines: (full.lines ?? []).map(x => ({ description: x.description, quantity: x.quantity, unitPrice: x.unitPrice })) };
      this.editingInvoiceId = i.id; this.showInvoiceForm = true;
    });
  }
  addLine(): void { this.invoiceForm.lines.push({ description: '', quantity: 1, unitPrice: 0 }); }
  removeLine(index: number): void { this.invoiceForm.lines.splice(index, 1); }
  async saveInvoice(): Promise<void> {
    const f = this.invoiceForm;
    if (!f.supplierId || !f.series.trim() || !f.number.trim() || !f.issueDate || !f.dueDate || f.dueDate < f.issueDate ||
        f.lines.length === 0 || f.lines.some(x => !x.description.trim() || Number(x.quantity) <= 0 || Number(x.unitPrice) < 0) || this.invoiceTotal <= 0) {
      this.error = 'Revisa proveedor, número, fechas y líneas. El total debe ser mayor que cero.'; return;
    }
    await this.run(async () => {
      if (this.editingInvoiceId) await this.api.put(`/invoices/${this.editingInvoiceId}`, f);
      else await this.api.post('/invoices', f);
      this.showInvoiceForm = false;
      this.invoices = await this.api.all<Invoice>('/invoices');
    }, 'Factura guardada.');
  }
  async cancelInvoice(i: Invoice): Promise<void> {
    if (!confirm(`¿Anular la factura ${i.series}-${i.number}?`)) return;
    await this.run(async () => {
      await this.api.post(`/invoices/${i.id}/cancel`, {});
      this.invoices = await this.api.all<Invoice>('/invoices');
    }, 'Factura anulada.');
  }

  newPayment(): void { this.paymentForm = blankPayment(); this.allocations = {}; this.showPaymentForm = true; }
  onPaymentMethodChanged(): void {
    this.paymentForm.check = this.selectedMethod?.requiresCheckDetails
      ? { bankName: '', checkNumber: '', clearedOn: null } : null;
  }
  async savePayment(): Promise<void> {
    const allocations: Allocation[] = Object.entries(this.allocations)
      .filter(([, amount]) => Number(amount) > 0)
      .map(([invoiceId, amount]) => ({ invoiceId: Number(invoiceId), amount: Number(amount) }));
    const amount = Math.round(this.paymentTotal * 100) / 100;
    if (!this.paymentForm.supplierId || !this.paymentForm.paymentMethodId || !this.paymentForm.date || allocations.length === 0 ||
        allocations.some(a => !this.payableInvoices.some(i => i.id === a.invoiceId && a.amount <= i.balance)) || amount <= 0 ||
        (this.selectedMethod?.requiresCheckDetails &&
          (!this.paymentForm.check?.bankName.trim() || !this.paymentForm.check?.checkNumber.trim()))) {
      this.error = 'Selecciona proveedor, método y al menos una factura. Cada abono debe respetar su saldo.'; return;
    }
    await this.run(async () => {
      await this.api.post('/payments', { ...this.paymentForm, amount, allocations,
        check: this.selectedMethod?.requiresCheckDetails ? this.paymentForm.check : null });
      this.showPaymentForm = false;
      [this.payments, this.invoices] = await Promise.all([
        this.api.all<Payment>('/payments'), this.api.all<Invoice>('/invoices')
      ]);
    }, 'Pago registrado.');
  }
  async viewPayment(p: Payment): Promise<void> {
    await this.run(async () => { this.selectedPayment = await this.api.get<Payment>(`/payments/${p.id}`); });
  }
  async cancelPayment(p: Payment): Promise<void> {
    if (!confirm(`¿Anular el pago #${p.id}? Se restaurarán los saldos de las facturas.`)) return;
    await this.run(async () => {
      await this.api.post(`/payments/${p.id}/cancel`, {});
      this.selectedPayment = null;
      [this.payments, this.invoices] = await Promise.all([
        this.api.all<Payment>('/payments'), this.api.all<Invoice>('/invoices')
      ]);
    }, 'Pago anulado.');
  }

  newAdjustment(): void {
    this.adjustmentForm = blankAdjustment();
    this.adjustmentAllocations = {};
    this.showAdjustmentForm = true;
  }
  async saveAdjustment(): Promise<void> {
    const f = this.adjustmentForm;
    const allocations: Allocation[] = Object.entries(this.adjustmentAllocations)
      .filter(([, amount]) => Number(amount) > 0)
      .map(([invoiceId, amount]) => ({ invoiceId: Number(invoiceId), amount: Number(amount) }));
    const amount = Math.round(this.adjustmentTotal * 100) / 100;
    if (!f.supplierId || !f.series.trim() || !f.number.trim() || !f.date || !f.description.trim() ||
        allocations.length === 0 || amount <= 0 ||
        allocations.some(a => !this.adjustmentInvoices.some(i => i.id === a.invoiceId &&
          (!this.isCreditAdjustment || a.amount <= i.balance)))) {
      this.error = 'Revisa proveedor, documento y aplicaciones. Los abonos no pueden superar el saldo.'; return;
    }
    await this.run(async () => {
      await this.api.post('/adjustments', { ...f, amount, allocations });
      this.showAdjustmentForm = false;
      [this.adjustments, this.invoices] = await Promise.all([
        this.api.all<Adjustment>('/adjustments'), this.api.all<Invoice>('/invoices')
      ]);
    }, 'Movimiento registrado.');
  }
  async viewAdjustment(a: Adjustment): Promise<void> {
    await this.run(async () => { this.selectedAdjustment = await this.api.get<Adjustment>(`/adjustments/${a.id}`); });
  }
  async cancelAdjustment(a: Adjustment): Promise<void> {
    if (!confirm(`¿Anular ${this.adjustmentLabel(a.kind)} ${a.series}-${a.number}?`)) return;
    await this.run(async () => {
      await this.api.post(`/adjustments/${a.id}/cancel`, {});
      this.selectedAdjustment = null;
      [this.adjustments, this.invoices] = await Promise.all([
        this.api.all<Adjustment>('/adjustments'), this.api.all<Invoice>('/invoices')
      ]);
    }, 'Movimiento anulado.');
  }

  private appliedFilters = { asOf: '', supplier: 'Todos', overdueOnly: false };
  async loadPayables(): Promise<void> {
    this.appliedFilters = {
      asOf: this.reportAsOf,
      supplier: this.suppliers.find(s => s.id === this.reportSupplierId)?.name ?? 'Todos',
      overdueOnly: this.reportOverdueOnly
    };
    [this.payableRows, this.supplierBalances] = await Promise.all([
      this.api.all<Invoice>('/reports/payables', {
        asOf: this.reportAsOf, supplierId: this.reportSupplierId || null, overdueOnly: this.reportOverdueOnly
      }),
      this.api.get<SupplierBalance[]>('/reports/supplier-balances', { asOf: this.reportAsOf })
    ]);
  }
  private pdf(report: Parameters<typeof exportPdf>[0]): Promise<void> {
    return this.run(() => exportPdf(report, this.auth.user()?.userName ?? ''));
  }
  async exportPayablesPdf(): Promise<void> {
    const f = this.appliedFilters;
    await this.pdf({
      title: 'Cuentas por pagar', fileName: `cuentas-por-pagar-${f.asOf}.pdf`,
      filters: [['Fecha de corte', f.asOf], ['Proveedor', f.supplier], ['Solo vencidas', f.overdueOnly ? 'Sí' : 'No']],
      head: ['Factura', 'Proveedor', 'Vence', 'Factura', 'Débitos', 'Créditos', 'Pagos', 'Saldo'],
      rows: this.payableRows.map(i => [`${i.series}-${i.number}`, i.supplierName, i.dueDate, this.money(i.total), this.money(i.debit), this.money(i.credit), this.money(i.paid), this.money(i.balance)]),
      rightColumns: [3, 4, 5, 6, 7],
      summary: [['Facturas', String(this.payableRows.length)], ['Total pendiente', this.money(this.payableTotal)]]
    });
  }
  async exportBalancesPdf(): Promise<void> {
    const total = this.supplierBalances.reduce((n, s) => n + s.outstanding, 0);
    await this.pdf({
      title: 'Balance por proveedor', fileName: `balance-proveedores-${this.appliedFilters.asOf}.pdf`,
      filters: [['Fecha de corte', this.appliedFilters.asOf]],
      head: ['Proveedor', 'Facturas pendientes', 'Vencido', 'Saldo total'],
      rows: this.supplierBalances.map(s => [s.supplierName, String(s.pendingInvoices), this.money(s.overdue), this.money(s.outstanding)]),
      rightColumns: [1, 2, 3],
      summary: [['Saldo total', this.money(total)]]
    });
  }
  async exportStatementPdf(): Promise<void> {
    const st = this.statement;
    if (!st) return;
    await this.pdf({
      title: 'Estado de cuenta', fileName: `estado-cuenta-${st.supplierId}-${st.from}-${st.to}.pdf`,
      filters: [['Proveedor', st.supplierName], ['Desde', st.from], ['Hasta', st.to]],
      head: ['Fecha', 'Tipo', 'Descripción', 'Cargo', 'Abono', 'Saldo'],
      rows: st.movements.map(m => [m.date, m.type, m.description, this.money(m.charge), this.money(m.credit), this.money(m.runningBalance)]),
      rightColumns: [3, 4, 5],
      summary: [['Saldo inicial', this.money(st.openingBalance)], ['Saldo final', this.money(st.closingBalance)]]
    });
  }
  async searchPayables(): Promise<void> { await this.run(() => this.loadPayables()); }
  async loadStatement(): Promise<void> {
    if (!this.reportSupplierId || !this.statementFrom || !this.statementTo || this.statementFrom > this.statementTo) {
      this.error = 'Selecciona un proveedor y un rango válido de fechas.'; return;
    }
    await this.run(async () => {
      this.statement = await this.api.get<Statement>(`/reports/suppliers/${this.reportSupplierId}/statement`,
        { from: this.statementFrom, to: this.statementTo });
    });
  }
  async downloadStatement(): Promise<void> {
    if (!this.statement) return;
    await this.run(async () => {
      const blob = await this.api.blob(`/reports/suppliers/${this.statement!.supplierId}/statement.csv`,
        { from: this.statement!.from, to: this.statement!.to });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `estado-cuenta-${this.statement!.supplierId}-${this.statement!.from}-${this.statement!.to}.csv`;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
  }

  editMethod(m: PaymentMethod): void {
    this.methodName = m.name;
    this.methodRequiresCheckDetails = m.requiresCheckDetails;
    this.editingMethodId = m.id;
  }
  async saveMethod(): Promise<void> {
    if (!this.methodName.trim()) { this.error = 'Escribe el nombre del método.'; return; }
    await this.run(async () => {
      const body = { name: this.methodName, requiresCheckDetails: this.methodRequiresCheckDetails };
      if (this.editingMethodId) await this.api.put(`/payment-methods/${this.editingMethodId}`, body);
      else await this.api.post('/payment-methods', body);
      this.methodName = ''; this.methodRequiresCheckDetails = false; this.editingMethodId = null;
      this.methods = await this.api.get<PaymentMethod[]>('/payment-methods');
    }, 'Método guardado.');
  }
  async toggleMethod(m: PaymentMethod): Promise<void> {
    await this.run(async () => {
      await this.api.put(`/payment-methods/${m.id}/status`, { isActive: !m.isActive });
      this.methods = await this.api.get<PaymentMethod[]>('/payment-methods');
    }, 'Estado actualizado.');
  }
  async createUser(): Promise<void> {
    if (!this.userName.trim() || this.userPassword.length < 12) { this.error = 'Indica usuario y contraseña de al menos 12 caracteres.'; return; }
    await this.run(async () => {
      await this.api.post('/users', { userName: this.userName.trim(), password: this.userPassword, role: this.userRole });
      this.userName = ''; this.userPassword = '';
      this.users = await this.api.get<User[]>('/users');
    }, 'Usuario creado.');
  }
  async toggleUser(u: User): Promise<void> {
    if (u.id === this.auth.user()?.id && u.isActive) { this.error = 'No puedes desactivar tu propia cuenta.'; return; }
    await this.run(async () => {
      await this.api.put(`/users/${u.id}/status`, { isActive: !u.isActive });
      this.users = await this.api.get<User[]>('/users');
    }, 'Estado actualizado.');
  }
  async changePassword(): Promise<void> {
    if (this.resetPassword.length < 12) { this.error = 'La contraseña debe tener al menos 12 caracteres.'; return; }
    await this.run(async () => {
      await this.api.put(`/users/${this.resetUserId}/password`, { password: this.resetPassword });
      this.resetUserId = ''; this.resetPassword = '';
    }, 'Contraseña actualizada.');
  }
}
