import { useMemo, useState } from "react";
import {
  ArrowLeft, ArrowRight, Briefcase, CalendarBlank, CaretDown, Check,
  CheckCircle, ClipboardText, Clock, Database, FlowArrow, FunnelSimple, Gauge,
  GearSix, HardDrives, House, IdentificationCard, ListChecks, LockKey,
  MagnifyingGlass, PaperPlaneTilt, PlugsConnected, Plus, Pulse, Receipt,
  Scroll, ShieldCheck, SignOut, SlidersHorizontal, Sparkle, Stack, UserPlus,
  Users, WarningCircle, WarningDiamond, X,
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

const adminNavItems = [
  { id: "admin-overview", label: "Vue d’ensemble", icon: Gauge },
  { id: "admin-access", label: "Accès", icon: ShieldCheck },
  { id: "admin-workflows", label: "Workflows", icon: FlowArrow },
  { id: "admin-operations", label: "Exploitation", icon: Pulse },
  { id: "admin-audit", label: "Audit", icon: Scroll },
  { id: "admin-settings", label: "Réglages", icon: SlidersHorizontal },
];

const isAdminPage = (page) => page.startsWith("admin-");

function AppLogo() {
  return <div className="brand" aria-label="Enterprise Workflow"><span className="brand-mark"><Sparkle weight="fill" size={20} /></span></div>;
}

function Sidebar({ page, onNavigate }) {
  const adminMode = isAdminPage(page);
  const items = adminMode ? adminNavItems : navItems;
  return (
    <aside className={`sidebar ${adminMode ? "admin-mode" : ""}`}>
      <AppLogo />
      <nav className="main-nav" aria-label="Navigation principale">
        {items.map(({ id, label, icon: Icon, count }) => {
          const active = page === id || (!adminMode && page === "detail" && id === "inbox") || (!adminMode && page === "new" && id === "start");
          return <button key={id} type="button" className={`nav-item ${active ? "active" : ""}`} onClick={() => onNavigate(id)}><span className="nav-icon"><Icon size={27} weight={active ? "fill" : "regular"} /></span><span>{label}</span>{count ? <span className="nav-count">{count}</span> : null}</button>;
        })}
      </nav>
    </aside>
  );
}

function Breadcrumb({ page, task, onNavigate }) {
  const [profileOpen, setProfileOpen] = useState(false);
  const adminMode = isAdminPage(page);
  const labels = { today: "Today", start: "Démarrer", new: "Nouvelle demande", inbox: "À traiter", detail: task?.id, requests: "Mes demandes", "admin-overview": "Vue d’ensemble", "admin-access": "Accès", "admin-workflows": "Workflows", "admin-operations": "Exploitation", "admin-audit": "Audit", "admin-settings": "Réglages" };
  const trail = adminMode ? ["Administration", labels[page]] : page === "detail" ? ["Home", "À traiter", task?.id] : page === "new" ? ["Home", "Démarrer", "Demande de congé"] : ["Home", labels[page]];
  const chooseMenu = (next) => { setProfileOpen(false); onNavigate(next); };
  return <header className="topbar"><div className="topbar-leading">{adminMode ? <button type="button" className="return-app-button" onClick={() => onNavigate("today")}><ArrowLeft size={16} /> Application</button> : null}<div className="breadcrumbs" aria-label="Fil d’Ariane">{trail.map((item, index) => <span key={`${item}-${index}`}>{index > 0 ? <span className="separator">/</span> : null}<button type="button" onClick={() => index === 0 && onNavigate(adminMode ? "admin-overview" : "today")}>{item}</button></span>)}</div></div><div className="top-profile-wrap">{profileOpen ? <div className="top-profile-menu" role="menu">{adminMode ? <button type="button" role="menuitem" onClick={() => chooseMenu("today")}><ArrowLeft size={17} /> Retour à l’application</button> : <button type="button" role="menuitem" onClick={() => chooseMenu("admin-overview")}><GearSix size={17} /> Administration</button>}<button type="button" role="menuitem"><SignOut size={17} /> Se déconnecter</button></div> : null}<button className="top-profile-button" type="button" onClick={() => setProfileOpen((open) => !open)} aria-expanded={profileOpen} aria-label="Ouvrir le menu utilisateur"><span className="avatar">OM</span><span className="profile-copy"><strong>Olivier M. Mukadi</strong><small>{adminMode ? "Mode administration" : "Administrateur"}</small></span><CaretDown size={15} /></button></div></header>;
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

const adminProfiles = [
  { id: "administrators", name: "Administrateurs", copy: "Configuration, accès et audit de l’installation.", identities: 2, groups: 0, workflows: 0, tone: "warning" },
  { id: "managers", name: "Managers", copy: "Approbation des demandes de congé et d’achat.", identities: 8, groups: 1, workflows: 2, tone: "progress" },
  { id: "backoffice", name: "Agents back-office", copy: "Saisie et traitement des opérations internes.", identities: 14, groups: 2, workflows: 3, tone: "progress" },
  { id: "validators", name: "Validateurs", copy: "Contrôle final avec séparation des acteurs.", identities: 5, groups: 1, workflows: 2, tone: "success" },
];

const adminIdentities = [
  { name: "Olivier M. Mukadi", email: "olivier.mukadi@enterprise.cd", provider: "Local", status: "Actif", profiles: "Administrateurs" },
  { name: "Amina Diallo", email: "amina.diallo@enterprise.cd", provider: "LDAP", status: "Actif", profiles: "Collaborateurs" },
  { name: "Jean Kabeya", email: "jean.kabeya@enterprise.cd", provider: "LDAP", status: "Actif", profiles: "Managers" },
  { name: "Sophie Kanza", email: "sophie.kanza@enterprise.cd", provider: "LDAP", status: "Actif", profiles: "Validateurs" },
];

const workflowStages = [
  { id: "submit", title: "Soumission de la demande", type: "Humain", actions: ["Soumettre"], recipient: "Initiateur", mode: "Identité désignée", detail: "Le demandeur complète et soumet son propre formulaire." },
  { id: "approve", title: "Approbation du superviseur", type: "Humain", actions: ["Consulter", "Approuver", "Refuser"], recipient: "Managers", mode: "Identité désignée", detail: "Une personne est choisie parmi les candidats habilités." },
  { id: "process", title: "Traitement back-office", type: "Humain", actions: ["Consulter", "Saisir", "Soumettre"], recipient: "Agents back-office", mode: "Pool éligible", detail: "Le premier agent habilité qui réclame la tâche la traite." },
  { id: "validate", title: "Validation finale", type: "Humain", actions: ["Consulter", "Valider", "Refuser"], recipient: "Validateurs", mode: "Pool éligible", detail: "Le validateur doit être distinct de l’auteur de l’action Saisir." },
  { id: "notify", title: "Notification", type: "Technique", actions: [], recipient: "Aucun participant", mode: "Automatique", detail: "Nœud technique exécuté par le moteur." },
];

const auditEntries = [
  { time: "Aujourd’hui, 10:42", actor: "Olivier M. Mukadi", action: "Habilitation ajoutée", target: "Managers · Approuver · congés v1.2", outcome: "Réussie" },
  { time: "Aujourd’hui, 09:18", actor: "Système", action: "État LDAP vérifié", target: "provider: corporate-ldap", outcome: "Réussie" },
  { time: "Hier, 17:06", actor: "Olivier M. Mukadi", action: "Session révoquée", target: "identity: ldap / jkabeya", outcome: "Réussie" },
  { time: "Hier, 15:31", actor: "Marie Tshibola", action: "Accès refusé", target: "security.audit.read", outcome: "Refusée" },
];

function AdminPageIntro({ eyebrow, title, copy, action }) {
  return <div className="admin-page-intro"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{copy}</p></div>{action}</div>;
}

function AdminOverview({ onNavigate }) {
  const health = [
    { label: "Sécurité", value: "Opérationnelle", copy: "LDAP et fournisseur interne disponibles", icon: ShieldCheck, tone: "success" },
    { label: "Workflows", value: "3 publiés", copy: "1 habilitation à compléter", icon: FlowArrow, tone: "warning" },
    { label: "Exécution", value: "Stable", copy: "Worker actif · aucun bail expiré", icon: Pulse, tone: "success" },
    { label: "Intégrations", value: "1 message en échec", copy: "Outbox · notification RH", icon: PlugsConnected, tone: "warning" },
  ];
  return <div className="content content-medium admin-content">
    <AdminPageIntro eyebrow="Centre de contrôle" title="Administration" copy="L’installation est opérationnelle. Deux éléments méritent votre attention." action={<Status tone="success">Système prêt</Status>} />
    <section className="admin-health-grid">{health.map(({ label, value, copy, icon: Icon, tone }) => <button type="button" className="health-card" key={label} onClick={() => onNavigate(label === "Sécurité" ? "admin-access" : label === "Workflows" ? "admin-workflows" : "admin-operations")}><span className={`health-icon ${tone}`}><Icon size={22} weight="duotone" /></span><small>{label}</small><strong>{value}</strong><p>{copy}</p><ArrowRight size={16} /></button>)}</section>
    <div className="admin-dashboard-grid">
      <section className="panel"><SectionHeading eyebrow="À vérifier" title="Requiert votre attention" /><div className="attention-list"><button type="button" onClick={() => onNavigate("admin-workflows")}><span className="attention-mark warning"><WarningDiamond size={19} weight="fill" /></span><span><strong>Un stage n’a aucun candidat direct</strong><small>Demande d’achat · Validation financière · version 2.0</small></span><ArrowRight size={16} /></button><button type="button" onClick={() => onNavigate("admin-operations")}><span className="attention-mark danger"><WarningCircle size={19} weight="fill" /></span><span><strong>Une livraison a épuisé ses tentatives</strong><small>Outbox · notification RH · il y a 22 minutes</small></span><ArrowRight size={16} /></button></div></section>
      <section className="panel"><SectionHeading eyebrow="Activité" title="Derniers changements" action={<button type="button" className="text-button" onClick={() => onNavigate("admin-audit")}>Tout voir <ArrowRight size={15} /></button>} /><div className="mini-audit">{auditEntries.slice(0, 3).map((entry) => <div key={`${entry.time}-${entry.action}`}><span className="audit-dot" /><p><strong>{entry.action}</strong><small>{entry.actor} · {entry.time}</small></p></div>)}</div></section>
    </div>
  </div>;
}

function AdminAccessPage() {
  const [tab, setTab] = useState("profiles");
  const [query, setQuery] = useState("");
  const [selectedProfile, setSelectedProfile] = useState(null);
  const visibleIdentities = adminIdentities.filter((identity) => `${identity.name} ${identity.email}`.toLowerCase().includes(query.toLowerCase()));
  return <div className="content content-medium admin-content">
    <AdminPageIntro eyebrow="Sécurité" title="Accès" copy="Gérez les profils, les identités et les fournisseurs de cette installation." action={tab === "profiles" ? <button type="button" className="primary-button" onClick={() => setSelectedProfile(adminProfiles[0])}><UserPlus size={17} /> Nouveau profil</button> : null} />
    <div className="segmented-control" role="tablist"><button type="button" className={tab === "profiles" ? "active" : ""} onClick={() => setTab("profiles")}>Profils</button><button type="button" className={tab === "identities" ? "active" : ""} onClick={() => setTab("identities")}>Identités</button><button type="button" className={tab === "providers" ? "active" : ""} onClick={() => setTab("providers")}>Fournisseurs</button></div>
    {tab === "profiles" ? <div className={`admin-master-detail ${selectedProfile ? "has-detail" : ""}`}><section className="panel admin-table"><div className="admin-table-head"><span>Profil</span><span>Membres</span><span>Workflows</span><span /></div>{adminProfiles.map((profile) => <button type="button" key={profile.id} className={selectedProfile?.id === profile.id ? "selected" : ""} onClick={() => setSelectedProfile(profile)}><span className="table-primary"><span className="row-icon"><LockKey size={20} /></span><span><strong>{profile.name}</strong><small>{profile.copy}</small></span></span><span>{profile.identities + profile.groups}</span><span>{profile.workflows || "—"}</span><ArrowRight size={16} /></button>)}</section>{selectedProfile ? <aside className="panel admin-inspector"><button type="button" className="close-decision" aria-label="Fermer le détail" onClick={() => setSelectedProfile(null)}><X size={18} /></button><span className="inspector-icon"><LockKey size={23} weight="duotone" /></span><p className="eyebrow">Profil interne</p><h2>{selectedProfile.name}</h2><p>{selectedProfile.copy}</p><dl className="inspector-stats"><div><dt>Identités</dt><dd>{selectedProfile.identities}</dd></div><div><dt>Groupes</dt><dd>{selectedProfile.groups}</dd></div><div><dt>Workflows</dt><dd>{selectedProfile.workflows}</dd></div></dl><div className="inspector-section"><strong>Permissions générales</strong><label><input type="checkbox" defaultChecked={selectedProfile.id === "administrators"} /> Gérer les accès</label><label><input type="checkbox" defaultChecked /> Lire les instances autorisées</label><label><input type="checkbox" defaultChecked={selectedProfile.id === "administrators"} /> Consulter l’audit</label></div><div className="inspector-section"><strong>Associations</strong><span className="soft-chip"><IdentificationCard size={14} /> {selectedProfile.identities} identités directes</span>{selectedProfile.groups ? <span className="soft-chip"><Users size={14} /> {selectedProfile.groups} groupe LDAP</span> : null}</div><button type="button" className="primary-button inspector-action">Modifier le profil</button></aside> : null}</div> : null}
    {tab === "identities" ? <><div className="toolbar panel"><label className="search-field"><MagnifyingGlass size={19} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Rechercher dans les fournisseurs autorisés" /></label><button type="button" className="filter-button"><FunnelSimple size={18} /> Tous les fournisseurs</button></div><section className="panel admin-table identity-table"><div className="admin-table-head"><span>Identité</span><span>Source</span><span>Profil</span><span>État</span></div>{visibleIdentities.map((identity) => <button type="button" key={identity.email}><span className="table-primary"><span className="avatar">{identity.name.split(" ").map((part) => part[0]).slice(0, 2).join("")}</span><span><strong>{identity.name}</strong><small>{identity.email}</small></span></span><span><span className="soft-chip">{identity.provider}</span></span><span>{identity.profiles}</span><span><Status tone="success">{identity.status}</Status></span></button>)}</section></> : null}
    {tab === "providers" ? <section className="provider-grid"><article className="panel provider-card"><div className="provider-card-top"><span className="provider-icon"><PlugsConnected size={24} /></span><Status tone="success">Actif</Status></div><h2>Corporate LDAP</h2><p>Annuaire principal en lecture seule, connecté en LDAPS.</p><div className="capability-list"><span>Recherche</span><span>Groupes</span><span>Statut des comptes</span></div><dl><div><dt>Identifiant</dt><dd>corporate-ldap</dd></div><div><dt>Serveur</dt><dd>ldaps://directory.enterprise.cd</dd></div><div><dt>Secret</dt><dd>Configuré · non affiché</dd></div></dl><button type="button" className="secondary-button">Consulter la configuration</button></article><article className="panel provider-card"><div className="provider-card-top"><span className="provider-icon"><HardDrives size={24} /></span><Status tone="success">Actif</Status></div><h2>Comptes locaux</h2><p>Fournisseur optionnel pour les comptes administrés dans l’installation.</p><div className="capability-list"><span>Création</span><span>Désactivation</span><span>Réinitialisation</span></div><dl><div><dt>Identifiant</dt><dd>local</dd></div><div><dt>Comptes actifs</dt><dd>2</dd></div><div><dt>Verrouillage</dt><dd>5 tentatives</dd></div></dl><button type="button" className="secondary-button">Gérer les comptes</button></article></section> : null}
  </div>;
}

function AdminWorkflowsPage() {
  const [selected, setSelected] = useState(null);
  const [openStage, setOpenStage] = useState("approve");
  const [modes, setModes] = useState({ approve: "Identité désignée", process: "Pool éligible", validate: "Pool éligible" });
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const updateMode = (stageId, mode) => { setModes((current) => ({ ...current, [stageId]: mode })); setDirty(true); setSaved(false); };
  if (!selected) return <div className="content content-medium admin-content"><AdminPageIntro eyebrow="Catalogue" title="Workflows" copy="Consultez les versions publiées et configurez leurs habilitations exactes." /><section className="panel workflow-admin-list"><div className="admin-table-head"><span>Workflow</span><span>Version active</span><span>Instances</span><span>État</span><span /></div><button type="button" onClick={() => setSelected("leave")}><span className="table-primary"><span className="row-icon"><CalendarBlank size={21} /></span><span><strong>Demande de congé</strong><small>leave-request · module RH</small></span></span><span>1.2.0</span><span>24 actives</span><Status tone="success">Prêt</Status><ArrowRight size={16} /></button><button type="button" onClick={() => setSelected("purchase")}><span className="table-primary"><span className="row-icon"><Receipt size={21} /></span><span><strong>Demande d’achat</strong><small>purchase-request · module Finance</small></span></span><span>2.0.0</span><span>8 actives</span><Status tone="warning">À compléter</Status><ArrowRight size={16} /></button><button type="button" onClick={() => setSelected("training")}><span className="table-primary"><span className="row-icon"><Users size={21} /></span><span><strong>Validation de formation</strong><small>training-approval · module RH</small></span></span><span>1.0.0</span><span>3 actives</span><Status tone="success">Prêt</Status><ArrowRight size={16} /></button></section></div>;
  return <div className="content content-medium admin-content workflow-detail-admin"><button type="button" className="back-button" onClick={() => setSelected(null)}><ArrowLeft size={17} /> Tous les workflows</button><AdminPageIntro eyebrow="leave-request · version 1.2.0" title="Demande de congé" copy="Configurez l’éligibilité de chaque action sans modifier la définition publiée." action={<Status tone="success">Version publiée</Status>} /><div className="workflow-summary panel"><div><span className="summary-icon"><Stack size={22} /></span><span><small>Module</small><strong>LeaveRequest 1.2.0</strong></span></div><div><span><small>Formulaire</small><strong>leave-form · v3</strong></span></div><div><span><small>Politique</small><strong>Révision 42</strong></span></div><div><span><small>Instances actives</small><strong>24</strong></span></div></div><div className="admin-subnav"><button type="button">Aperçu</button><button type="button" className="active">Stages et accès</button><button type="button">Versions</button><button type="button">Instances</button><button type="button">Diagnostics</button></div><section className="stage-list" aria-label="Stages du workflow">{workflowStages.map((stage, index) => { const expanded = openStage === stage.id; const actualMode = modes[stage.id] || stage.mode; return <article className={`panel stage-card ${expanded ? "expanded" : ""}`} key={stage.id}><button type="button" className="stage-summary" onClick={() => setOpenStage(expanded ? null : stage.id)}><span className={`stage-index ${stage.type === "Technique" ? "technical" : ""}`}>{stage.type === "Technique" ? <GearSix size={16} /> : index + 1}</span><span className="stage-main"><small>{stage.type} · {stage.id}</small><strong>{stage.title}</strong><span>{stage.detail}</span></span><span className="stage-meta"><small>Affectation</small><strong>{actualMode}</strong></span><CaretDown size={17} className={expanded ? "rotated" : ""} /></button>{expanded ? <div className="stage-editor">{stage.type === "Technique" ? <div className="readonly-note"><GearSix size={18} /><span><strong>Nœud automatique</strong><small>Aucun participant ni habilitation humaine n’est requis.</small></span></div> : <><div className="editor-block"><label>Mode d’affectation</label><div className="choice-cards"><button type="button" className={actualMode === "Identité désignée" ? "selected" : ""} onClick={() => updateMode(stage.id, "Identité désignée")}><IdentificationCard size={20} /><span><strong>Identité désignée</strong><small>Une personne choisie parmi les candidats éligibles.</small></span></button><button type="button" className={actualMode === "Pool éligible" ? "selected" : ""} onClick={() => updateMode(stage.id, "Pool éligible")}><Users size={20} /><span><strong>Pool éligible</strong><small>Claim exclusif par le premier participant autorisé.</small></span></button></div></div><div className="editor-block"><label>Habilitations exactes</label><div className="grant-table"><div><span>Action</span><span>Destinataire</span><span>Portée</span><span /></div>{stage.actions.map((action) => <div key={action}><strong>{action}</strong><span className="soft-chip"><LockKey size={13} /> {stage.recipient}</span><small>v1.2.0 · {stage.id}</small><button type="button" className="icon-button" aria-label={`Retirer ${action}`} onClick={() => setDirty(true)}><X size={15} /></button></div>)}</div><button type="button" className="text-button" onClick={() => setDirty(true)}><Plus size={15} /> Ajouter une habilitation</button></div>{stage.id === "validate" ? <div className="policy-note"><ShieldCheck size={18} /><span><strong>Séparation des acteurs active</strong><small>Le validateur doit être distinct de l’auteur de l’action « Saisir ».</small></span></div> : null}</>}</div> : null}</article>; })}</section>{saved ? <div className="inline-success"><CheckCircle size={18} weight="fill" /> Politique enregistrée · révision 43</div> : null}{dirty ? <div className="admin-savebar"><div><strong>Modifications non enregistrées</strong><small>Les grants seront revalidés immédiatement à la prochaine commande.</small></div><div><button type="button" className="secondary-button" onClick={() => setDirty(false)}>Annuler</button><button type="button" className="primary-button" onClick={() => { setDirty(false); setSaved(true); }}>Enregistrer les changements</button></div></div> : null}</div>;
}

function AdminOperationsPage() {
  const [tab, setTab] = useState("instances");
  return <div className="content content-medium admin-content"><AdminPageIntro eyebrow="Exploitation" title="État de l’installation" copy="Diagnostiquez les exécutions et les composants sans modifier leur état arbitrairement." action={<Status tone="success">Readiness positive</Status>} /><section className="operations-strip"><div><span className="health-pulse" /><span><small>Host</small><strong>Disponible</strong></span></div><div><Pulse size={20} /><span><small>Worker</small><strong>Actif · concurrence 4</strong></span></div><div><Database size={20} /><span><small>Base</small><strong>Oracle 19.19</strong></span></div><div><PlugsConnected size={20} /><span><small>Outbox</small><strong>1 échec final</strong></span></div></section><div className="segmented-control" role="tablist"><button type="button" className={tab === "instances" ? "active" : ""} onClick={() => setTab("instances")}>Instances</button><button type="button" className={tab === "outbox" ? "active" : ""} onClick={() => setTab("outbox")}>Outbox</button><button type="button" className={tab === "modules" ? "active" : ""} onClick={() => setTab("modules")}>Modules</button></div>{tab === "instances" ? <section className="panel admin-table operations-table"><div className="admin-table-head"><span>Instance</span><span>Workflow</span><span>Stage actif</span><span>Statut</span><span /></div>{[{ id: "WF-8F31A", workflow: "Demande de congé", stage: "Approbation", status: "En attente", tone: "progress" }, { id: "WF-8F305", workflow: "Demande d’achat", stage: "Validation financière", status: "En attente", tone: "warning" }, { id: "WF-8F2E8", workflow: "Validation de formation", stage: "Notification", status: "En cours", tone: "progress" }].map((item) => <button type="button" key={item.id}><span className="table-primary"><span className="row-icon"><FlowArrow size={20} /></span><span><strong>{item.id}</strong><small>Démarrée aujourd’hui</small></span></span><span>{item.workflow}</span><span>{item.stage}</span><Status tone={item.tone}>{item.status}</Status><ArrowRight size={16} /></button>)}</section> : null}{tab === "outbox" ? <section className="panel admin-table operations-table"><div className="admin-table-head"><span>Opération</span><span>Destination</span><span>Tentatives</span><span>État</span><span /></div><button type="button"><span className="table-primary"><span className="row-icon error"><WarningCircle size={20} /></span><span><strong>Notifier les Ressources Humaines</strong><small>OP-00918 · dernière tentative il y a 22 min</small></span></span><span>hr-notifications</span><span>10 / 10</span><Status tone="warning">Échec final</Status><ArrowRight size={16} /></button><button type="button"><span className="table-primary"><span className="row-icon"><CheckCircle size={20} /></span><span><strong>Notifier le demandeur</strong><small>OP-00917 · livrée à 10:38</small></span></span><span>email-service</span><span>1 / 10</span><Status tone="success">Livrée</Status><ArrowRight size={16} /></button></section> : null}{tab === "modules" ? <section className="panel admin-table operations-table"><div className="admin-table-head"><span>Module</span><span>Version</span><span>Workflows</span><span>État</span><span /></div>{[{ name: "LeaveRequest", version: "1.2.0", count: 1 }, { name: "Purchasing", version: "2.0.0", count: 1 }, { name: "Training", version: "1.0.0", count: 1 }].map((module) => <button type="button" key={module.name}><span className="table-primary"><span className="row-icon"><Stack size={20} /></span><span><strong>{module.name}</strong><small>Empreinte vérifiée</small></span></span><span>{module.version}</span><span>{module.count}</span><Status tone="success">Compatible</Status><ArrowRight size={16} /></button>)}</section> : null}<p className="operations-footnote"><LockKey size={15} /> Les déploiements, migrations et restaurations restent réalisés par l’exploitant selon le runbook.</p></div>;
}

function AdminAuditPage() {
  const [query, setQuery] = useState("");
  const entries = auditEntries.filter((entry) => `${entry.actor} ${entry.action} ${entry.target}`.toLowerCase().includes(query.toLowerCase()));
  return <div className="content content-medium admin-content"><AdminPageIntro eyebrow="Traçabilité" title="Audit" copy="Consultez les mutations critiques et les événements de sécurité de l’installation." /><div className="toolbar panel"><label className="search-field"><MagnifyingGlass size={19} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Rechercher un acteur, une action ou une ressource" /></label><button type="button" className="filter-button"><CalendarBlank size={18} /> 7 derniers jours</button><button type="button" className="filter-button"><FunnelSimple size={18} /> Résultat</button></div><section className="panel audit-table"><div className="admin-table-head"><span>Date</span><span>Acteur et action</span><span>Ressource</span><span>Résultat</span><span /></div>{entries.map((entry) => <button type="button" key={`${entry.time}-${entry.action}`}><span><strong>{entry.time.split(", ")[0]}</strong><small>{entry.time.split(", ")[1]}</small></span><span><strong>{entry.action}</strong><small>{entry.actor}</small></span><span>{entry.target}</span><Status tone={entry.outcome === "Réussie" ? "success" : "warning"}>{entry.outcome}</Status><ArrowRight size={16} /></button>)}</section><p className="operations-footnote"><IdentificationCard size={15} /> L’audit fonctionnel est séparé des logs techniques et n’affiche aucun secret.</p></div>;
}

function AdminSettingsPage() {
  const [saved, setSaved] = useState(false);
  const [revalidate, setRevalidate] = useState(true);
  return <div className="content content-small admin-content"><AdminPageIntro eyebrow="Installation" title="Réglages" copy="Personnalisez l’interface et consultez la configuration effective." /><section className="panel settings-panel"><SectionHeading eyebrow="Apparence" title="Identité de l’installation" /><div className="settings-fields"><label>Nom affiché<input defaultValue="Enterprise Workflow" /></label><label>Fuseau horaire<select defaultValue="Africa/Kinshasa"><option>Africa/Kinshasa</option><option>UTC</option></select></label><div className="setting-row"><span><strong>Couleur d’accent</strong><small>Utilisée pour les actions et les états sélectionnés.</small></span><span className="color-swatch"><i /> #1768E5</span></div></div></section><section className="panel settings-panel"><SectionHeading eyebrow="Sessions" title="Politique de sécurité" /><div className="settings-fields"><div className="field-grid"><label>Inactivité maximale<select defaultValue="30"><option value="30">30 minutes</option><option value="60">1 heure</option></select></label><label>Durée maximale<select defaultValue="8"><option value="8">8 heures</option><option value="12">12 heures</option></select></label></div><div className="setting-row"><span><strong>Revalidation du statut distant</strong><small>Vérifier périodiquement qu’une identité LDAP reste active.</small></span><button type="button" className={`switch ${revalidate ? "on" : ""}`} role="switch" aria-checked={revalidate} onClick={() => setRevalidate((value) => !value)}><span /></button></div></div></section><section className="panel settings-panel readonly-settings"><SectionHeading eyebrow="Configuration effective" title="Runtime" action={<Status tone="progress">Lecture seule</Status>} /><dl><div><dt>Stockage</dt><dd>Oracle · schéma EW_PROD</dd></div><div><dt>Concurrence worker</dt><dd>4</dd></div><div><dt>Bail / renouvellement</dt><dd>30 s / 10 s</dd></div><div><dt>Timeout handler</dt><dd>5 minutes</dd></div></dl><p><GearSix size={15} /> Ces paramètres sont validés au démarrage et modifiés par l’exploitant.</p></section>{saved ? <div className="inline-success"><CheckCircle size={18} weight="fill" /> Réglages enregistrés</div> : null}<div className="settings-actions"><button type="button" className="primary-button" onClick={() => setSaved(true)}>Enregistrer les réglages</button></div></div>;
}

export function App() {
  const [page, setPage] = useState("today");
  const [selectedTask, setSelectedTask] = useState(tasks[0]);
  const navigate = (next) => { setPage(next); window.scrollTo({ top: 0, behavior: "smooth" }); };
  const openTask = (task) => { setSelectedTask(task); navigate("detail"); };
  const handlePointerMove = (event) => { const rect = event.currentTarget.getBoundingClientRect(); event.currentTarget.style.setProperty("--pointer-x", `${event.clientX - rect.left}px`); event.currentTarget.style.setProperty("--pointer-y", `${event.clientY - rect.top}px`); };
  return <div className="app-shell"><Sidebar page={page} onNavigate={navigate} /><main className="workspace" onPointerMove={handlePointerMove}><div className="grid-reveal" aria-hidden="true" /><Breadcrumb page={page} task={selectedTask} onNavigate={navigate} /><div className="page-stage">{page === "today" ? <TodayPage onNavigate={navigate} onOpenTask={openTask} /> : null}{page === "inbox" ? <InboxPage onOpenTask={openTask} /> : null}{page === "detail" ? <DetailPage task={selectedTask} onNavigate={navigate} /> : null}{page === "start" ? <StartPage onStart={() => navigate("new")} /> : null}{page === "new" ? <NewRequestPage onNavigate={navigate} /> : null}{page === "requests" ? <RequestsPage /> : null}{page === "admin-overview" ? <AdminOverview onNavigate={navigate} /> : null}{page === "admin-access" ? <AdminAccessPage /> : null}{page === "admin-workflows" ? <AdminWorkflowsPage /> : null}{page === "admin-operations" ? <AdminOperationsPage /> : null}{page === "admin-audit" ? <AdminAuditPage /> : null}{page === "admin-settings" ? <AdminSettingsPage /> : null}</div></main></div>;
}
