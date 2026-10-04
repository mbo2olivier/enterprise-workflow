import { useMemo, useState } from "react";
import {
  ArrowLeft, ArrowRight, Briefcase, CalendarBlank, CaretDown, Check,
  CheckCircle, ClipboardText, Clock, FunnelSimple, GearSix,
  House, ListChecks, MagnifyingGlass, PaperPlaneTilt, Plus, Receipt,
  SignOut, Sparkle, Users, WarningCircle, X,
} from "@phosphor-icons/react";

const tasks = [
  { id: "REQ-124355", title: "Demande de congé", requester: "Amina Diallo", date: "Aujourd’hui, 09:14", urgency: "À traiter aujourd’hui", icon: CalendarBlank, detail: "Congé familial" },
  { id: "REQ-124351", title: "Demande d’achat", requester: "Thomas Mvumbi", date: "Hier, 16:27", urgency: "À traiter aujourd’hui", icon: Receipt, detail: "Matériel informatique" },
  { id: "REQ-124339", title: "Validation de formation", requester: "Sophie Kanza", date: "1 oct. 2026", urgency: "Cette semaine", icon: Users, detail: "Plan de formation 2026" },
];

const requests = [
  { id: "REQ-124284", title: "Demande de congé", date: "28 sept. 2026", status: "Approuvée", tone: "success" },
  { id: "REQ-124211", title: "Accès à une application", date: "24 sept. 2026", status: "En cours", tone: "progress" },
  { id: "REQ-124105", title: "Achat d’équipement", date: "16 sept. 2026", status: "Informations requises", tone: "warning" },
];

const catalog = [
  { title: "Demande de congé", copy: "Soumettre une période d’absence à votre responsable.", icon: CalendarBlank },
  { title: "Demande d’achat", copy: "Faire valider un achat ou un nouvel équipement.", icon: Receipt },
  { title: "Accès à une application", copy: "Demander l’accès à un outil de l’organisation.", icon: Briefcase },
];

const navItems = [
  { id: "today", label: "Today", icon: House },
  { id: "start", label: "Démarrer", icon: Plus },
  { id: "inbox", label: "À traiter", icon: ListChecks, count: 3 },
  { id: "requests", label: "Mes demandes", icon: ClipboardText },
];

function AppLogo() {
  return <div className="brand" aria-label="Enterprise Workflow"><span className="brand-mark"><Sparkle weight="fill" size={20} /></span></div>;
}

function Sidebar({ page, onNavigate }) {
  return (
    <aside className="sidebar">
      <AppLogo />
      <nav className="main-nav" aria-label="Navigation principale">
        {navItems.map(({ id, label, icon: Icon, count }) => {
          const active = page === id || (page === "detail" && id === "inbox") || (page === "new" && id === "start");
          return <button key={id} type="button" className={`nav-item ${active ? "active" : ""}`} onClick={() => onNavigate(id)}><span className="nav-icon"><Icon size={27} weight={active ? "fill" : "regular"} /></span><span>{label}</span>{count ? <span className="nav-count">{count}</span> : null}</button>;
        })}
      </nav>
    </aside>
  );
}

function Breadcrumb({ page, task, onNavigate }) {
  const [profileOpen, setProfileOpen] = useState(false);
  const labels = { today: "Today", start: "Démarrer", new: "Nouvelle demande", inbox: "À traiter", detail: task?.id, requests: "Mes demandes" };
  const trail = page === "detail" ? ["Home", "À traiter", task?.id] : page === "new" ? ["Home", "Démarrer", "Demande de congé"] : ["Home", labels[page]];
  return <header className="topbar"><div className="breadcrumbs" aria-label="Fil d’Ariane">{trail.map((item, index) => <span key={`${item}-${index}`}>{index > 0 ? <span className="separator">/</span> : null}<button type="button" onClick={() => index === 0 && onNavigate("today")}>{item}</button></span>)}</div><div className="top-profile-wrap">{profileOpen ? <div className="top-profile-menu" role="menu"><button type="button" role="menuitem"><GearSix size={17} /> Administration</button><button type="button" role="menuitem"><SignOut size={17} /> Se déconnecter</button></div> : null}<button className="top-profile-button" type="button" onClick={() => setProfileOpen((open) => !open)} aria-expanded={profileOpen} aria-label="Ouvrir le menu utilisateur"><span className="avatar">OM</span><span className="profile-copy"><strong>Olivier M. Mukadi</strong><small>Administrateur</small></span><CaretDown size={15} /></button></div></header>;
}

function SectionHeading({ eyebrow, title, action }) {
  return <div className="section-heading"><div>{eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}<h2>{title}</h2></div>{action}</div>;
}

function Status({ tone = "progress", children }) {
  const icons = { success: CheckCircle, warning: WarningCircle, progress: Clock };
  const Icon = icons[tone];
  return <span className={`status ${tone}`}><Icon size={15} weight="fill" />{children}</span>;
}

function TodayPage({ onNavigate, onOpenTask }) {
  return (
    <div className="content content-narrow">
      <div className="page-intro"><p className="eyebrow">Samedi 3 octobre</p><h1>Hello, Olivier M. Mukadi</h1><p>Voici ce qui mérite votre attention aujourd’hui.</p></div>
      <section className="panel focus-panel">
        <SectionHeading eyebrow="Votre journée" title="À faire maintenant" action={<button className="text-button" type="button" onClick={() => onNavigate("inbox")}>Tout voir <ArrowRight size={16} /></button>} />
        <div className="stack-list">{tasks.slice(0, 2).map((task, index) => <button className="list-row" type="button" key={task.id} onClick={() => onOpenTask(task)}><span className="row-icon"><task.icon size={21} /></span><span className="row-main"><strong>{task.title}</strong><small>{task.requester} · {task.detail}</small></span><span className="row-meta"><Status tone={index === 0 ? "warning" : "progress"}>{task.urgency}</Status><small>{task.date}</small></span><ArrowRight size={17} /></button>)}</div>
      </section>
      <section className="panel"><SectionHeading title="Mes demandes récentes" action={<button className="text-button" type="button" onClick={() => onNavigate("requests")}>Tout afficher <ArrowRight size={16} /></button>} /><div className="stack-list compact">{requests.slice(0, 2).map((request) => <button className="list-row" type="button" key={request.id} onClick={() => onNavigate("requests")}><span className="row-icon"><ClipboardText size={20} /></span><span className="row-main"><strong>{request.title}</strong><small>{request.id} · {request.date}</small></span><Status tone={request.tone}>{request.status}</Status><ArrowRight size={17} /></button>)}</div></section>
    </div>
  );
}

function InboxPage({ onOpenTask }) {
  const [query, setQuery] = useState("");
  const [onlyUrgent, setOnlyUrgent] = useState(false);
  const filtered = useMemo(() => tasks.filter((task) => `${task.title} ${task.requester} ${task.detail}`.toLowerCase().includes(query.toLowerCase()) && (!onlyUrgent || task.urgency.includes("aujourd’hui"))), [query, onlyUrgent]);
  return (
    <div className="content content-medium">
      <div className="page-intro row-title"><div><p className="eyebrow">Votre file</p><h1>À traiter</h1><p>{filtered.length} requêtes nécessitent votre intervention.</p></div></div>
      <div className="toolbar panel"><label className="search-field"><MagnifyingGlass size={19} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Rechercher une requête" /></label><button type="button" className={`filter-button ${onlyUrgent ? "selected" : ""}`} onClick={() => setOnlyUrgent((value) => !value)}><FunnelSimple size={18} weight={onlyUrgent ? "fill" : "regular"} /> Aujourd’hui</button></div>
      <section className="panel request-list" aria-live="polite">{filtered.length ? filtered.map((task) => <article className="request-card" key={task.id}><div className="request-leading"><span className="row-icon large"><task.icon size={23} /></span><div><small className="request-id">{task.id}</small><h3>{task.title}</h3><p>{task.requester} · {task.detail}</p></div></div><div className="request-trailing"><Status tone={task.urgency.includes("aujourd’hui") ? "warning" : "progress"}>{task.urgency}</Status><small>{task.date}</small><button type="button" className="secondary-button" onClick={() => onOpenTask(task)}>Consulter <ArrowRight size={16} /></button></div></article>) : <div className="empty-state"><CheckCircle size={32} weight="duotone" /><h3>Aucune requête trouvée</h3><p>Modifiez votre recherche ou retirez le filtre.</p></div>}</section>
    </div>
  );
}

function DetailPage({ task, onNavigate }) {
  const [decision, setDecision] = useState(null);
  const [reason, setReason] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const activeTask = task || tasks[0];
  const chooseDecision = (next) => { setDecision(next); setReason(""); setTimeout(() => document.getElementById("decision-form")?.scrollIntoView({ behavior: "smooth", block: "center" }), 20); };
  const submit = (event) => { event.preventDefault(); if (decision === "reject" && !reason.trim()) return; setSubmitted(true); };
  if (submitted) return <div className="content content-small success-view"><span className="success-mark"><Check size={34} weight="bold" /></span><p className="eyebrow">Décision enregistrée</p><h1>{decision === "approve" ? "La demande est validée" : "La demande est rejetée"}</h1><p>La requête {activeTask.id} a été mise à jour et le demandeur sera notifié.</p><button type="button" className="primary-button" onClick={() => onNavigate("inbox")}>Revenir à la file</button></div>;
  return (
    <div className="detail-layout">
      <div className="content content-small detail-content">
        <button type="button" className="back-button" onClick={() => onNavigate("inbox")}><ArrowLeft size={17} /> Retour à la file</button>
        <div className="page-intro detail-title"><p className="eyebrow">{activeTask.id}</p><h1>{activeTask.title}</h1><p>{activeTask.requester} · Équipe Marketing</p></div>
        <section className="panel detail-panel"><SectionHeading title="Détails" action={<Status tone="warning">En attente de votre décision</Status>} /><dl className="detail-list"><div><dt>Demandeur</dt><dd>{activeTask.requester}</dd></div><div><dt>Période</dt><dd>12–16 octobre 2026</dd></div><div><dt>Durée</dt><dd>5 jours ouvrés</dd></div><div><dt>Motif</dt><dd>{activeTask.detail}</dd></div><div className="wide"><dt>Message</dt><dd>Je souhaite poser ces jours afin de m’occuper de ma famille. Merci pour votre retour.</dd></div></dl></section>
        <section className="panel progress-panel"><p className="panel-kicker">Avancement</p><div className="progress-track"><div className="progress-step done"><span><Check size={13} weight="bold" /></span><div><strong>Reçue</strong><small>3 oct., 09:14</small></div></div><div className="progress-line active" /><div className="progress-step current"><span /><div><strong>Validation</strong><small>En cours</small></div></div><div className="progress-line" /><div className="progress-step"><span /><div><strong>Notification</strong><small>À venir</small></div></div></div></section>
        {decision ? <section className="panel decision-panel" id="decision-form"><button type="button" className="close-decision" aria-label="Fermer" onClick={() => setDecision(null)}><X size={18} /></button><p className="panel-kicker">Réaliser la demande</p><h2>{decision === "approve" ? "Valider cette demande" : "Rejeter cette demande"}</h2><p>{decision === "approve" ? "Vous pouvez ajouter une note avant de confirmer." : "Indiquez la raison du rejet pour informer le demandeur."}</p><form onSubmit={submit}><label htmlFor="decision-reason">{decision === "approve" ? "Note (facultative)" : "Raison du rejet"}</label><textarea id="decision-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder={decision === "approve" ? "Ajouter une note…" : "Saisir une raison…"} required={decision === "reject"} /><button type="submit" className={`primary-button ${decision === "reject" ? "danger" : ""}`}><PaperPlaneTilt size={17} weight="fill" /> Confirmer</button></form></section> : null}
      </div>
      <div className="action-dock"><div><strong>Votre décision</strong><small>Elle sera enregistrée dans l’historique.</small></div><div className="action-buttons"><button type="button" className={`secondary-button ${decision === "reject" ? "selected-danger" : ""}`} onClick={() => chooseDecision("reject")}>Rejeter</button><button type="button" className="primary-button" onClick={() => chooseDecision("approve")}><Check size={17} weight="bold" /> Valider</button></div></div>
    </div>
  );
}

function StartPage({ onStart }) {
  return <div className="content content-medium"><div className="page-intro"><p className="eyebrow">Catalogue</p><h1>Démarrer une demande</h1><p>Choisissez le processus qui correspond à votre besoin.</p></div><section className="catalog-grid">{catalog.map((item, index) => <button className="catalog-card" type="button" key={item.title} onClick={() => index === 0 && onStart()}><span className="catalog-icon"><item.icon size={25} /></span><span><strong>{item.title}</strong><small>{item.copy}</small></span><ArrowRight size={18} /></button>)}</section></div>;
}

function NewRequestPage({ onNavigate }) {
  const [sent, setSent] = useState(false);
  const [form, setForm] = useState({ start: "2026-10-12", end: "2026-10-16", reason: "Congé familial", message: "" });
  const update = (key, value) => setForm((current) => ({ ...current, [key]: value }));
  if (sent) return <div className="content content-small success-view"><span className="success-mark"><Check size={34} weight="bold" /></span><p className="eyebrow">Demande envoyée</p><h1>Votre demande a été créée</h1><p>Vous pourrez suivre son avancement dans Mes demandes.</p><button className="primary-button" type="button" onClick={() => onNavigate("requests")}>Voir mes demandes</button></div>;
  return (
    <div className="content content-small">
      <button type="button" className="back-button" onClick={() => onNavigate("start")}><ArrowLeft size={17} /> Retour au catalogue</button>
      <div className="page-intro"><p className="eyebrow">Nouvelle demande</p><h1>Demande de congé</h1><p>Indiquez la période souhaitée et le motif de votre demande.</p></div>
      <form className="panel request-form" onSubmit={(event) => { event.preventDefault(); setSent(true); }}><fieldset><legend>Période</legend><div className="field-grid"><label>Date de début<input type="date" value={form.start} onChange={(event) => update("start", event.target.value)} required /></label><label>Date de fin<input type="date" value={form.end} onChange={(event) => update("end", event.target.value)} required /></label></div><p className="field-hint">5 jours ouvrés</p></fieldset><fieldset><legend>Détails</legend><label>Motif<input value={form.reason} onChange={(event) => update("reason", event.target.value)} required /></label><label>Message pour le responsable <span>Facultatif</span><textarea value={form.message} onChange={(event) => update("message", event.target.value)} placeholder="Ajouter un contexte utile…" /></label></fieldset><div className="form-actions"><button className="secondary-button" type="button" onClick={() => onNavigate("start")}>Annuler</button><span>Vous pourrez suivre son avancement dans Mes demandes.</span><button className="primary-button" type="submit"><PaperPlaneTilt size={17} weight="fill" /> Envoyer la demande</button></div></form>
    </div>
  );
}

function RequestsPage() {
  return <div className="content content-medium"><div className="page-intro"><p className="eyebrow">Suivi</p><h1>Mes demandes</h1><p>Retrouvez les processus que vous avez démarrés.</p></div><section className="panel request-list">{requests.map((request) => <article className="request-card simple" key={request.id}><div className="request-leading"><span className="row-icon large"><ClipboardText size={22} /></span><div><small className="request-id">{request.id}</small><h3>{request.title}</h3><p>Démarrée le {request.date}</p></div></div><div className="request-trailing"><Status tone={request.tone}>{request.status}</Status><button className="icon-button" type="button" aria-label={`Ouvrir ${request.id}`}><ArrowRight size={18} /></button></div></article>)}</section></div>;
}

export function App() {
  const [page, setPage] = useState("today");
  const [selectedTask, setSelectedTask] = useState(tasks[0]);
  const navigate = (next) => { setPage(next); window.scrollTo({ top: 0, behavior: "smooth" }); };
  const openTask = (task) => { setSelectedTask(task); navigate("detail"); };
  const handlePointerMove = (event) => { const rect = event.currentTarget.getBoundingClientRect(); event.currentTarget.style.setProperty("--pointer-x", `${event.clientX - rect.left}px`); event.currentTarget.style.setProperty("--pointer-y", `${event.clientY - rect.top}px`); };
  return <div className="app-shell"><Sidebar page={page} onNavigate={navigate} /><main className="workspace" onPointerMove={handlePointerMove}><div className="grid-reveal" aria-hidden="true" /><Breadcrumb page={page} task={selectedTask} onNavigate={navigate} /><div className="page-stage">{page === "today" ? <TodayPage onNavigate={navigate} onOpenTask={openTask} /> : null}{page === "inbox" ? <InboxPage onOpenTask={openTask} /> : null}{page === "detail" ? <DetailPage task={selectedTask} onNavigate={navigate} /> : null}{page === "start" ? <StartPage onStart={() => navigate("new")} /> : null}{page === "new" ? <NewRequestPage onNavigate={navigate} /> : null}{page === "requests" ? <RequestsPage /> : null}</div></main></div>;
}
