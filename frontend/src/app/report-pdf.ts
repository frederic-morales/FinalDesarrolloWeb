const GREEN: [number, number, number] = [18, 62, 53];
const MINT: [number, number, number] = [180, 226, 202];

export interface PdfReport {
  title: string;
  fileName: string;
  filters: [string, string][];
  head: string[];
  rows: string[][];
  rightColumns: number[];
  summary?: [string, string][];
}

export async function exportPdf(report: PdfReport, user: string): Promise<void> {
  const [{ jsPDF }, autoTableModule] = await Promise.all([import('jspdf'), import('jspdf-autotable')]);
  // El paquete es CommonJS: según el bundler la función queda en default o en default.default.
  const mod = autoTableModule as unknown as { default: unknown };
  const autoTable = (typeof mod.default === 'function' ? mod.default : (mod.default as { default: unknown }).default) as
    typeof import('jspdf-autotable').default;
  const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'letter' });
  const width = doc.internal.pageSize.getWidth();
  const height = doc.internal.pageSize.getHeight();

  const header = () => {
    doc.setFillColor(...GREEN);
    doc.rect(0, 0, width, 62, 'F');
    doc.setFillColor(...MINT);
    doc.roundedRect(36, 14, 34, 34, 6, 6, 'F');
    doc.setTextColor(...GREEN);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(15);
    doc.text('CP', 53, 36, { align: 'center' });
    doc.setTextColor(255, 255, 255);
    doc.setFontSize(14);
    doc.text('Cuentas por pagar', 82, 29);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.setTextColor(...MINT);
    doc.text('Panel de control', 82, 43);
    doc.setTextColor(255, 255, 255);
    doc.setFontSize(10);
    doc.text(report.title, width - 36, 36, { align: 'right' });
  };

  header();
  let y = 84;
  doc.setTextColor(40, 60, 52);
  doc.setFontSize(9);
  const filterText = report.filters.map(([k, v]) => `${k}: ${v}`).join('   |   ');
  doc.setFont('helvetica', 'bold');
  doc.text('Filtros aplicados', 36, y);
  doc.setFont('helvetica', 'normal');
  y += 13;
  doc.text(doc.splitTextToSize(filterText, width - 72), 36, y);

  autoTable(doc, {
    head: [report.head],
    body: report.rows.length ? report.rows : [[{ content: 'Sin resultados para estos filtros.', colSpan: report.head.length, styles: { halign: 'center' } } as never]],
    startY: y + 16,
    margin: { left: 36, right: 36, top: 76, bottom: 40 },
    styles: { fontSize: 8, cellPadding: 4, textColor: [60, 80, 72] },
    headStyles: { fillColor: GREEN, textColor: 255, fontStyle: 'bold' },
    alternateRowStyles: { fillColor: [246, 249, 247] },
    columnStyles: Object.fromEntries(report.rightColumns.map(c => [c, { halign: 'right' }])),
    didDrawPage: data => { if (data.pageNumber > 1) header(); }
  });

  if (report.summary?.length) {
    let sy = ((doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY) + 22;
    if (sy > height - 60) { doc.addPage(); header(); sy = 90; }
    doc.setFontSize(10);
    for (const [k, v] of report.summary) {
      doc.setFont('helvetica', 'normal');
      doc.setTextColor(90, 110, 100);
      doc.text(k, width - 200, sy, { align: 'right' });
      doc.setFont('helvetica', 'bold');
      doc.setTextColor(...GREEN);
      doc.text(v, width - 36, sy, { align: 'right' });
      sy += 15;
    }
  }

  const pages = doc.getNumberOfPages();
  const stamp = new Date().toLocaleString('es-GT');
  for (let i = 1; i <= pages; i++) {
    doc.setPage(i);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(140, 155, 148);
    doc.text(`Generado el ${stamp} por ${user}`, 36, height - 20);
    doc.text(`Página ${i} de ${pages}`, width - 36, height - 20, { align: 'right' });
  }
  doc.save(report.fileName);
}
