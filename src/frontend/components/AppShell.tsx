'use client';
import Link from 'next/link';
import {useEffect,useState} from 'react';
import {api,getSession,logout} from '@/lib/api';
import {usePathname,useRouter} from 'next/navigation';

type Access={status:string;isReadOnly:boolean;message:string;accessEndsAt:string|null};
type NavItem={href:string;label:string};
type NavGroup={label:string;items:NavItem[];admin?:boolean};

const groups:NavGroup[]=[
 {label:'Overview',items:[{href:'/dashboard',label:'Dashboard'}]},
 {label:'Events',items:[{href:'/events',label:'Events workspace'},{href:'/event-staffing',label:'Staff & attendance'},{href:'/logistics',label:'Logistics'},{href:'/event-reports',label:'Event reports'}]},
 {label:'Finance',items:[{href:'/event-finance',label:'Event finance'},{href:'/invoices',label:'Invoices'},{href:'/purchasing',label:'Purchasing'},{href:'/accounting',label:'Accounting'},{href:'/expenses',label:'Expenses'}]},
 {label:'Contacts & stock',items:[{href:'/customers',label:'Customers'},{href:'/products',label:'Products'}]},
 {label:'Administration',admin:true,items:[{href:'/team',label:'Team & activity'},{href:'/subscription',label:'Subscription'},{href:'/settings',label:'Company settings'}]}
];

export default function AppShell({children}:{children:React.ReactNode}){
 const router=useRouter(),pathname=usePathname(),[name,setName]=useState(''),[admin,setAdmin]=useState(false),[access,setAccess]=useState<Access|null>(null),[menuOpen,setMenuOpen]=useState(false),[openGroups,setOpenGroups]=useState<string[]>([]);
 useEffect(()=>{const s=getSession();if(!s)router.replace('/login');else{setName(s.user.firstName);setAdmin(s.user.role==='Admin');api<Access>('/subscription/access').then(setAccess).catch(()=>{})}},[router]);
 useEffect(()=>{const current=groups.find(g=>g.items.some(i=>pathname.startsWith(i.href)));if(current)setOpenGroups(x=>x.includes(current.label)?x:[...x,current.label])},[pathname]);
 function toggle(label:string){setOpenGroups(x=>x.includes(label)?x.filter(v=>v!==label):[...x,label])}
 return <div className="shell">
  {menuOpen&&<button className="nav-backdrop" aria-label="Close menu" onClick={()=>setMenuOpen(false)}/>}
  <aside className={`side ${menuOpen?'open':''}`}>
   <div className="brand-row"><Link className="brand" href="/dashboard">Lanka<span>SaaS</span></Link><button className="icon-btn mobile-only" aria-label="Close menu" onClick={()=>setMenuOpen(false)}>&times;</button></div>
   <nav className="nav" aria-label="Main navigation">{groups.filter(g=>!g.admin||admin).map(group=>{const expanded=openGroups.includes(group.label);return <section className="nav-group" key={group.label}><button className="nav-group-button" aria-expanded={expanded} onClick={()=>toggle(group.label)}><span>{group.label}</span><span>{expanded?'−':'+'}</span></button>{expanded&&<div className="nav-items">{group.items.map(item=><Link key={item.href} href={item.href} className={pathname.startsWith(item.href)?'active':''} onClick={()=>setMenuOpen(false)}>{item.label}</Link>)}</div>}</section>})}</nav>
   <div className="side-footer"><span className="avatar">{name.slice(0,1).toUpperCase()}</span><div><strong>{name||'Account'}</strong><small>{admin?'Administrator':'Team member'}</small></div></div>
  </aside>
  <main className="main"><header className="top"><button className="icon-btn mobile-only" aria-label="Open menu" onClick={()=>setMenuOpen(true)}>☰</button><div className="top-spacer"/><span className="top-name">{name}</span><button className="btn secondary compact" onClick={async()=>{await logout();router.push('/login')}}>Log out</button></header>{access&&(access.isReadOnly||access.status==='PastDue'||access.status==='Cancelled')&&<div className={`access-banner ${access.isReadOnly?'blocked':'warning'}`}><span>{access.message}</span>{admin&&<Link href="/subscription">Manage billing</Link>}</div>}<div className="content">{children}</div></main>
 </div>;
}
