'use client';
import Link from 'next/link';
import {useEffect,useState} from 'react';
import {api,getSession} from '@/lib/api';

type Data={totalSales:number;totalExpenses:number;customers:number;products:number};
const money=(value:number)=>new Intl.NumberFormat('en-LK',{style:'currency',currency:'LKR',maximumFractionDigits:0}).format(value);

export default function Dashboard(){
 const[d,setD]=useState<Data|null>(null),[error,setError]=useState('');
 const admin=getSession()?.user.role==='Admin';
 useEffect(()=>{api<Data>('/dashboard').then(setD).catch(e=>setError(e.message))},[]);
 const cards=d?[['Sales',money(d.totalSales),'Recorded customer revenue'],['Expenses',money(d.totalExpenses),'Recorded business costs'],['Customers',String(d.customers),'Customer records'],['Products',String(d.products),'Active catalogue items']]:[];
 return <><div className="heading"><div><h1>Good day{getSession()?.user.firstName?`, ${getSession()?.user.firstName}`:''}</h1><p className="muted">Start with today&apos;s event operations and financial follow-ups.</p></div></div>{error&&<p className="error">{error}</p>}{!d&&!error?<div className="grid">{[1,2,3,4].map(x=><div className="card metric" key={x}><span className="muted">Loading…</span><strong>—</strong></div>)}</div>:<div className="grid">{cards.map(([label,value,note])=><div className="card metric" key={label}><span className="muted">{label}</span><strong>{value}</strong><small className="muted">{note}</small></div>)}</div>}
 <section className="section"><div className="section-heading"><div><h2>Quick actions</h2><p className="muted">Common tasks without hunting through menus.</p></div></div><div className="quick-actions"><Link className="btn" href="/events?new=1">Create event</Link><Link className="btn secondary" href="/customers">Add customer</Link><Link className="btn secondary" href="/event-staffing">Attendance</Link><Link className="btn secondary" href="/logistics">Dispatch & returns</Link><Link className="btn secondary" href="/event-finance">Event finance</Link></div></section>
 <section className="section"><div className="panel"><div className="section-heading"><div><h2>Operations checklist</h2><p className="muted">A simple rhythm for every event day.</p></div></div><div className="dashboard-checklist"><Link href="/event-staffing"><strong>1. Confirm staff</strong><span>Review assignments and on-site check-ins</span></Link><Link href="/logistics"><strong>2. Confirm logistics</strong><span>Review dispatches, returns and shortages</span></Link><Link href="/event-finance"><strong>3. Review money</strong><span>Record deposits, costs and supplier payments</span></Link><Link href="/event-reports"><strong>4. Review outcome</strong><span>Check event profit and outstanding balances</span></Link></div></div></section>
 {admin&&<section className="section"><div className="panel"><div className="section-heading"><div><h2>Administration</h2><p className="muted">Manage access and company configuration.</p></div></div><div className="quick-actions"><Link className="btn secondary" href="/team">Manage team</Link><Link className="btn secondary" href="/settings">Company settings</Link><Link className="btn secondary" href="/subscription">Subscription</Link></div></div></section>}</>;
}
