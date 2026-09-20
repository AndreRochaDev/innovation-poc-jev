export interface Channel { key: string; label_pt: string }
export interface Department { key: string; label_pt: string; description: string }
export interface Priority { key: string; label_pt: string; description: string }
export interface Taxonomy { channels: Channel[]; departments: Department[]; priorities: Priority[]; model: string }
export interface TriageResult {
  departamento: string; departamentoLabel: string; confianca: number;
  probabilidades: Record<string, number>;
  urgente: boolean; probUrgente: number;
  frustracao: string; frustracaoProbs?: Record<string, number> | null;
  prioridade: string; slaSugerido: string; equipaSugerida: string;
  confiancaBaixa: boolean; modelo: string; canal: string;
}

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export async function getTaxonomy(): Promise<Taxonomy> {
  const r = await fetch(`${API}/api/taxonomy`);
  if (!r.ok) throw new Error(`taxonomy ${r.status}`);
  return r.json();
}

export async function triageJson(channel: string, text: string): Promise<TriageResult> {
  const r = await fetch(`${API}/api/triage`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ channel, text }),
  });
  const data = await r.json();
  if (!r.ok) throw new Error(data?.error ?? `erro ${r.status}`);
  return data;
}

export async function triageForm(channel: string, text: string, file?: File | null): Promise<TriageResult> {
  const fd = new FormData();
  fd.append("channel", channel);
  fd.append("text", text);
  if (file) fd.append("file", file);
  const r = await fetch(`${API}/api/classify`, { method: "POST", body: fd });
  const data = await r.json();
  if (!r.ok) throw new Error(data?.error ?? `erro ${r.status}`);
  return data;
}
