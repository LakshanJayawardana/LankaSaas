'use client';
import Link from 'next/link';
import {useEffect,useState} from 'react';
import {api,getSession,logout} from '@/lib/api';
import {usePathname,useRouter} from 'next/navigation';

type Access={status:string;isReadOnly:boolean;message:string;accessEndsAt:string|null};
type Branding={businessName:string;logoUrl?:string|null};
type Profile={firstName:string;lastName:string;role:string;profilePhotoUrl?:string|null};
type NavItem={href:string;label:string};
type NavGroup={label:string;items:NavItem[];admin?:boolean};

const groups:NavGroup[]=[
 {label:'Overview',items:[{href:'/dashboard',label:'Dashboard'}]},
 {label:'Events',items:[{href:'/events',label:'Events workspace'},{href:'/event-staffing',label:'Staff & attendance'},{href:'/logistics',label:'Logistics'},{href:'/event-reports',label:'Event reports'}]},
 {label:'Finance',items:[{href:'/event-finance',label:'Event finance'},{href:'/invoices',label:'Invoices'},{href:'/purchasing',label:'Purchasing'},{href:'/accounting',label:'Accounting'},{href:'/expenses',label:'Expenses'}]},
 {label:'Contacts & stock',items:[{href:'/customers',label:'Customers'},{href:'/products',label:'Products'}]},
 {label:'Administration',admin:true,items:[{href:'/team',label:'Team & activity'},{href:'/subscription',label:'Subscription'},{href:'/settings',label:'Company settings'}]}
];

function Avatar({profile,className='avatar'}:{profile:Profile|null;className?:string}){
 const initial=(profile?.firstName?.[0]||'A').toUpperCase();
 return <span className={className}>{profile?.profilePhotoUrl&&<img src={profile.profilePhotoUrl} alt="" onError={e=>{e.currentTarget.style.display='none'}}/>}<span>{initial}</span></span>;
}

export default function AppShell({children}:{children:React.ReactNode}){
 const router=useRouter(),pathname=usePathname(),[profile,setProfile]=useState<Profile|null>(null),[branding,setBranding]=useState<Branding|null>(null),[admin,setAdmin]=useState(false),[access,setAccess]=useState<Access|null>(null),[menuOpen,setMenuOpen]=useState(false),[openGroups,setOpenGroups]=useState<string[]>([]);
 useEffect(()=>{const session=getSession();if(!session){router.replace('/login');return}setAdmin(session.user.role==='Admin');const load=()=>Promise.all([api<Profile>('/profile'),api<Branding>('/settings'),api<Access>('/subscription/access')]).then(([person,company,subscription])=>{setProfile(person);setBranding(company);setAccess(subscription)}).catch(()=>{});void load();window.addEventListener('profile-updated',load);window.addEventListener('branding-updated',load);return()=>{window.removeEventListener('profile-updated',load);window.removeEventListener('branding-updated',load)}},[router]);
 useEffect(()=>{const current=groups.find(g=>g.items.some(i=>pathname.startsWith(i.href)));if(current)setOpenGroups(x=>x.includes(current.label)?x:[...x,current.label])},[pathname]);
 function toggle(label:string){setOpenGroups(x=>x.includes(label)?x.filter(v=>v!==label):[...x,label])}
 const name=profile?.firstName||getSession()?.user.firstName||'';
 return <div className="shell">
  {menuOpen&&<button className="nav-backdrop" aria-label="Close menu" onClick={()=>setMenuOpen(false)}/>}
  <aside className={`side ${menuOpen?'open':''}`}>
   <div className="brand-row"><Link className="tenant-brand" href="/dashboard">{branding?.logoUrl&&<img src={branding.logoUrl} alt={`${branding.businessName} logo`} onError={e=>{e.currentTarget.style.display='none'}}/>}<span>{branding?.businessName||<>Lanka<strong>SaaS</strong></>}</span></Link><button className="icon-btn mobile-only" aria-label="Close menu" onClick={()=>setMenuOpen(false)}>&times;</button></div>
   <nav className="nav" aria-label="Main navigation">{groups.filter(g=>!g.admin||admin).map(group=>{const expanded=openGroups.includes(group.label);return <section className="nav-group" key={group.label}><button className="nav-group-button" aria-expanded={expanded} onClick={()=>toggle(group.label)}><span>{group.label}</span><span>{expanded?'−':'+'}</span></button>{expanded&&<div className="nav-items">{group.items.map(item=><Link key={item.href} href={item.href} className={pathname.startsWith(item.href)?'active':''} onClick={()=>setMenuOpen(false)}>{item.label}</Link>)}</div>}</section>})}</nav>
   <Link className="side-footer" href="/profile"><Avatar profile={profile}/><div><strong>{name||'Account'}</strong><small>{profile?.role==='Admin'?'Administrator':'Team member'}</small></div></Link>
  </aside>
  <main className="main"><header className="top"><button className="icon-btn mobile-only" aria-label="Open menu" onClick={()=>setMenuOpen(true)}>☰</button><div className="top-spacer"/><Link className="top-profile" href="/profile"><Avatar profile={profile} className="top-avatar"/><span>{name}</span></Link><button className="btn secondary compact" onClick={async()=>{await logout();router.push('/login')}}>Log out</button></header>{access&&(access.isReadOnly||access.status==='PastDue'||access.status==='Cancelled')&&<div className={`access-banner ${access.isReadOnly?'blocked':'warning'}`}><span>{access.message}</span>{admin&&<Link href="/subscription">Manage billing</Link>}</div>}<div className="content">{children}</div></main>
 </div>;
}
