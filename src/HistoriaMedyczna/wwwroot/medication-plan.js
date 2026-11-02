export function printPlan() {
    if (document.getElementById('medication-plan-report')) window.print();
}

export async function downloadPlan() {
    const report = document.getElementById('medication-plan-report');
    if (!report) return;
    const response = await fetch(new URL('./medication-plan-report.css', import.meta.url));
    if (!response.ok) throw new Error('Nie udało się pobrać stylów listy leków.');
    const css = await response.text();
    const html = '<!doctype html><html lang="pl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Lista leków</title><style>' + css + '</style></head><body>' + report.outerHTML + '</body></html>';
    const url = URL.createObjectURL(new Blob([html], { type: 'text/html;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url; link.download = 'lista-lekow.html';
    document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
