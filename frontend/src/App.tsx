import { useEffect, useState } from "react";
import { getTaxonomy, triageForm, type Taxonomy, type TriageResult } from "./lib/api";
import "./App.css";

export default function App() {
  const [tax, setTax] = useState<Taxonomy | null>(null);
  const [channel, setChannel] = useState("portal");
  const [text, setText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<TriageResult | null>(null);

  useEffect(() => {
    getTaxonomy().then(setTax).catch((e) => setError(`Backend indisponível: ${e.message}. Verifique VITE_API_URL.`));
  }, []);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setResult(null);
    if (!text.trim() && !file) { setError("Cole o texto da reclamação ou anexe um ficheiro .txt/.md/.eml."); return; }
    setLoading(true);
    try {
      const r = await triageForm(channel, text, file);
      setResult(r);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }

  function loadSample(kind: string) {
    const samples: Record<string, { channel: string; text: string }> = {
      portal: { channel: "portal", text: "Há três dias que o contentor do meu prédio na Rua das Flores está cheio e o lixo espalhado pelo passeio. Já liguei duas vezes e ninguém resolveu. Cheira muito mal." },
      email: { channel: "email", text: "Exmos. Senhores, venho reportar uma fuga de água grave na Rua Central nº 12. A água corre pela estrada desde ontem à noite. Penso que há risco de infiltração nas garagens." },
      telefone: { channel: "telefone", text: "Transcrição chamada: munícipe muito irritado, diz que poste em frente à escola está apagado há uma semana, crianças atravessam no escuro, exige resolução hoje." },
      balcao: { channel: "balcao", text: "Munícipe deslocou-se ao balcão: buraco grande no passeio frente ao centro de saúde, senhora idosa caiu ontem. Pede reparação urgente." },
      redes: { channel: "redes_sociais", text: "@municipio vergonha! obra ao lado sem licença a fazer barulho às 7h da manhã ao fim de semana!! Ninguém fiscaliza??" },
    };
    const s = samples[kind];
    if (s) { setChannel(s.channel); setText(s.text); setFile(null); }
  }

  const topProbs = result
    ? Object.entries(result.probabilidades ?? {}).sort((a, b) => b[1] - a[1]).slice(0, 3)
    : [];

  return (
    <div className="page">
      <header className="header">
        <div>
          <h1>Triagem de Reclamações — POC JEV</h1>
          <p>Município demo · JEV via OpenCode Zen · <code>{tax?.model ?? "…"}</code></p>
        </div>
        <div className="badge">PT-PT · POC · sem dados reais</div>
      </header>

      <main className="grid">
        <section className="card">
          <h2>1. Entrada multicanal (simulada)</h2>
          <form onSubmit={onSubmit}>
            <label>Canal
              <select value={channel} onChange={(e) => setChannel(e.target.value)}>
                {(tax?.channels ?? [{ key: "portal", label_pt: "Portal" }]).map((c) => (
                  <option key={c.key} value={c.key}>{c.label_pt}</option>
                ))}
              </select>
            </label>
            <label>Texto da reclamação
              <textarea rows={8} value={text} onChange={(e) => setText(e.target.value)}
                placeholder="Cole aqui o texto do portal, email, transcrição telefónica…" />
            </label>
            <label>Ficheiro (.txt/.md/.eml — PDF ainda não suportado nesta POC)
              <input type="file" accept=".txt,.md,.eml,.json,.csv,.log"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            </label>
            <div className="samples">
              <span>Exemplos:</span>
              {["portal", "email", "telefone", "balcao", "redes"].map((k) => (
                <button key={k} type="button" onClick={() => loadSample(k)}>{k}</button>
              ))}
            </div>
            <button type="submit" disabled={loading}>{loading ? "A classificar…" : "Classificar com JEV"}</button>
          </form>
          {error && <p className="error">{error}</p>}
        </section>

        <section className="card">
          <h2>2. Resultado da triagem</h2>
          {!result && <p className="muted">Submeta uma reclamação para ver departamento, urgência, prioridade e SLA.</p>}
          {result && (
            <div className="result">
              <div className={`prio prio-${result.prioridade}`}>{result.prioridade} · SLA {result.slaSugerido}</div>
              <h3>{result.departamentoLabel}</h3>
              <p>Confiança: <b>{(result.confianca * 100).toFixed(1)}%</b>
                {result.confiancaBaixa && <span className="warn"> — confiança baixa, rever manualmente</span>}</p>
              <p>Urgente: <b>{result.urgente ? "Sim" : "Não"}</b> ({(result.probUrgente * 100).toFixed(0)}%) ·
                Frustração: <b>{result.frustracao}</b></p>
              <p>Equipa sugerida: <b>{result.equipaSugerida}</b> · Canal: {result.canal} · Modelo: {result.modelo}</p>
              <h4>Top departamentos</h4>
              <ul>{topProbs.map(([k, v]) => <li key={k}>{k} — {(v * 100).toFixed(1)}%</li>)}</ul>
            </div>
          )}
        </section>
      </main>

      <footer className="footer">
        <p>Taxonomia configurável em <code>appsettings.json</code> · Chave <code>OPENCODE_API_KEY</code> só no backend ·
          Não usar dados pessoais reais (modelos Zen alojados nos EUA).</p>
      </footer>
    </div>
  );
}
