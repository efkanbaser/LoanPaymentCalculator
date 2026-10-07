"use strict";
const form = document.querySelector("#loan-form");
const errorBox = document.querySelector("#form-error");
const submit = document.querySelector("#calculate");
const results = document.querySelector("#results");
const staleNotice = document.querySelector("#stale-notice");
const exports = ["export-csv", "export-tsv", "print"].map(id => document.getElementById(id));
const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 2 });
const exportNumber = new Intl.NumberFormat("tr-TR", { useGrouping: false, minimumFractionDigits: 2, maximumFractionDigits: 2 });
const presets = {
  standard: { term: 24, principalGrace: 0, interestGrace: 0, paymentInterval: 1, annualRate: 36 },
  grace: { term: 24, principalGrace: 6, interestGrace: 3, paymentInterval: 1, annualRate: 36 },
  quarter: { term: 24, principalGrace: 0, interestGrace: 0, paymentInterval: 3, annualRate: 36 },
  zero: { term: 12, principalGrace: 0, interestGrace: 0, paymentInterval: 1, annualRate: 0 }
};
let currentPlan = null;
let requestNumber = 0;
let controller;
const text = (id, value) => { document.getElementById(id).textContent = value; };
const dateLabel = value => value.split("-").reverse().join(".");
const unitLabel = { Ay: "ay", "Gün": "gün", "Yıl": "yıl" };

function readInputs() {
  const data = new FormData(form);
  const holidays = String(data.get("holidays") || "").split(/[,;\s]+/).filter(Boolean);
  if (holidays.some(date => !/^\d{4}-\d{2}-\d{2}$/.test(date)))
    throw new Error("Tatil tarihlerini YYYY-AA-GG biçiminde girin; örneğin 2026-10-29.");
  return {
    principal: Number(data.get("principal")), annualRate: Number(data.get("annualRate")),
    term: Number(data.get("term")), period: String(data.get("period")),
    paymentInterval: Number(data.get("paymentInterval")), principalGrace: Number(data.get("principalGrace")),
    interestGrace: Number(data.get("interestGrace")), kkdf: Number(data.get("kkdf")), bsmv: Number(data.get("bsmv")),
    startDate: String(data.get("startDate")), shiftBusinessDays: data.has("shiftBusinessDays"), holidays
  };
}

function markStale() {
  if (currentPlan) staleNotice.hidden = false;
  exports.forEach(button => { button.disabled = true; });
  text("interval-unit", `${unitLabel[form.elements.period.value]}da bir`);
}
form.addEventListener("input", () => {
  markStale();
  document.querySelectorAll(".preset").forEach(button => button.classList.remove("active"));
});
form.addEventListener("change", markStale);
document.querySelectorAll(".preset").forEach(button => button.addEventListener("click", () => {
  Object.entries(presets[button.dataset.preset]).forEach(([key, value]) => { form.elements[key].value = value; });
  form.elements.period.value = "Ay";
  document.querySelectorAll(".preset").forEach(item => item.classList.toggle("active", item === button));
  markStale();
  if (form.reportValidity()) calculate();
}));
form.addEventListener("submit", event => { event.preventDefault(); calculate(); });

async function calculate() {
  const thisRequest = ++requestNumber;
  controller?.abort();
  controller = new AbortController();
  submit.disabled = true;
  results.setAttribute("aria-busy", "true");
  errorBox.hidden = true;
  exports.forEach(button => { button.disabled = true; });
  try {
    const input = readInputs();
    const response = await fetch("/api/schedule", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(input), signal: controller.signal
    });
    if (!response.ok) {
      const details = await response.json().catch(() => ({}));
      throw new Error(details.error || "Girdileri kontrol edin. Tarihler ve sayılar geçerli olmalı.");
    }
    const schedule = await response.json();
    if (thisRequest !== requestNumber) return;
    currentPlan = schedule;
    render(schedule);
    // An edit during an in-flight request must not make the displayed plan look current.
    const unchanged = JSON.stringify(input) === JSON.stringify(readInputs());
    staleNotice.hidden = unchanged;
    exports.forEach(button => { button.disabled = !unchanged; });
  } catch (error) {
    if (error.name === "AbortError" || thisRequest !== requestNumber) return;
    errorBox.textContent = error.message === "Failed to fetch" ? "Hesaplama servisine erişilemiyor. Tekrar deneyin." : error.message;
    errorBox.hidden = false;
    if (currentPlan) staleNotice.hidden = false;
  } finally {
    if (thisRequest === requestNumber) {
      submit.disabled = false;
      results.setAttribute("aria-busy", "false");
    }
  }
}

function render(schedule) {
  const { inputs, rows } = schedule;
  text("total-payment", currency.format(schedule.totalPayment));
  text("total-principal", currency.format(schedule.totalPrincipal));
  text("total-interest", currency.format(schedule.totalInterest));
  text("total-tax", currency.format(schedule.totalTaxComponent));
  text("first-last", `İlk ödeme ${currency.format(schedule.firstPayment)} · Son ödeme ${currency.format(schedule.lastPayment)}`);
  text("plan-caption", `${inputs.term} ${unitLabel[inputs.period]} · ${rows.length} ödeme · %${inputs.annualRate} yıllık`);
  const ratio = Math.min(1, Math.max(0, schedule.totalPrincipal / schedule.totalPayment));
  text("principal-ratio", `%${Math.round(ratio * 100)}`);
  document.getElementById("principal-ring").style.strokeDashoffset = String(301.6 * (1 - ratio));
  text("table-caption", `${dateLabel(rows[0].date)} — ${dateLabel(rows.at(-1).date)} · ${rows.length} ödeme`);
  const body = document.getElementById("payment-rows");
  const fragment = document.createDocumentFragment();
  rows.forEach(row => {
    const tr = document.createElement("tr");
    [String(row.number), dateLabel(row.date), ...[row.payment, row.principal, row.interest, row.taxComponent, row.balance].map(value => currency.format(value))]
      .forEach((value, index) => {
        const td = document.createElement("td");
        td.textContent = value;
        if (index === 0 && row.principal === 0) {
          const badge = document.createElement("span"); badge.className = "interest-only"; badge.textContent = "faiz"; td.append(badge);
        }
        tr.append(td);
      });
    fragment.append(tr);
  });
  body.replaceChildren(fragment);
  drawChart(inputs.principal, rows);
  text("chart-start", dateLabel(inputs.startDate));
  text("chart-end", dateLabel(rows.at(-1).date));
}

function drawChart(principal, rows) {
  const width = 660, height = 160, left = 56, right = 8, top = 9, bottom = 15;
  const values = [principal, ...rows.map(row => row.balance)];
  const maximum = Math.max(...values, 1);
  const x = index => left + index * (width - left - right) / (values.length - 1);
  const y = value => top + (1 - value / maximum) * (height - top - bottom);
  const points = values.map((value, index) => `${x(index).toFixed(2)},${y(value).toFixed(2)}`).join(" ");
  const short = value => new Intl.NumberFormat("tr-TR", { notation: "compact", maximumFractionDigits: 1 }).format(value);
  const grid = [0, .5, 1].map(fraction => {
    const ordinate = y(maximum * fraction).toFixed(2);
    return `<line class="chart-grid" x1="${left}" y1="${ordinate}" x2="${width - right}" y2="${ordinate}"/><text class="chart-axis" x="0" y="${Number(ordinate) + 4}">${short(maximum * fraction)} ₺</text>`;
  }).join("");
  // All values in this markup are finite numbers from the typed calculation response.
  document.getElementById("chart").innerHTML = `<svg viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" role="img" aria-label="Ödeme sırasına göre kalan bakiye, Türk lirası"><title>Ödeme sırasına göre kalan anapara (₺)</title>${grid}<polygon class="chart-area" points="${left},${height - bottom} ${points} ${width - right},${height - bottom}"/><polyline class="chart-line" points="${points}"/></svg>`;
}

function download(format) {
  if (!currentPlan) return;
  const delimiter = format === "csv" ? ";" : "\t";
  const header = ["No", "Ödeme tarihi", "Taksit", "Anapara", "Faiz", "Vergi bileşeni", "Kalan bakiye"];
  const records = currentPlan.rows.map(row => [String(row.number), dateLabel(row.date),
    ...[row.payment, row.principal, row.interest, row.taxComponent, row.balance].map(value => exportNumber.format(value))]);
  const data = [header, ...records].map(record => record.join(delimiter)).join("\r\n");
  const url = URL.createObjectURL(new Blob(["\ufeff", data], { type: "text/plain;charset=utf-8" }));
  const anchor = document.createElement("a"); anchor.href = url; anchor.download = `loan-plan-${currentPlan.inputs.startDate}.${format}`;
  document.body.append(anchor); anchor.click(); anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
document.getElementById("export-csv").addEventListener("click", () => download("csv"));
document.getElementById("export-tsv").addEventListener("click", () => download("tsv"));
document.getElementById("print").addEventListener("click", () => window.print());
calculate();
