'use client';
import Link from 'next/link';
import {useEffect,useState} from 'react';
import {api,getSession,logout} from '@/lib/api';
import {usePathname,useRouter} from 'next/navigation';

type Access={status:string;isReadOnly:boolean;message:string;accessEndsAt:string|null};
type Branding={businessName:string;logoUrl?:string|null};
type Profile={firstName:string;lastName:string;role:string;profilePhotoUrl?:string|null};
type PermissionAccess={isAdministrator:boolean;permissions:string[]};
type NavItem={href:string;label:string;permission?:string};
type NavGroup={label:string;items:NavItem[]};

const groups:NavGroup[]=[
 {label:'Overview',items:[{href:'/dashboard',label:'Dashboard'}]},
 {label:'Events',items:[{href:'/events',label:'Events workspace',permission:'events.view'},{href:'/event-staffing',label:'Staff & attendance',permission:'staffing.view'},{href:'/logistics',label:'Logistics',permission:'logistics.view'},{href:'/event-reports',label:'Event reports',permission:'finance.view'}]},
 {label:'Finance',items:[{href:'/event-finance',label:'Event finance',permission:'finance.view'},{href:'/invoices',label:'Invoices',permission:'finance.view'},{href:'/purchasing',label:'Purchasing',permission:'purchasing.view'},{href:'/accounting',label:'Accounting',permission:'accounting.view'},{href:'/expenses',label:'Expenses',permission:'finance.view'}]},
 {label:'Contacts & stock',items:[{href:'/customers',label:'Customers',permission:'contacts.view'},{href:'/products',label:'Products',permission:'logistics.view'}]},
 {label:'Administration',items:[{href:'/team',label:'Team & activity',permission:'administration.users'},{href:'/departments',label:'Departments & access',permission:'administration.users'},{href:'/subscription',label:'Subscription',permission:'administration.billing'},{href:'/settings',label:'Company settings',permission:'administration.settings'}]}
];

function Avatar({profile,className='avatar'}:{profile:Profile|null;className?:string}){
 const initial=(profile?.firstName?.[0]||'A').toUpperCase();
 return <span className={className}>{profile?.profilePhotoUrl&&<img src={profile.profilePhotoUrl} alt="" onError={e=>{e.currentTarget.style.display='none'}}/>}<span>{initial}</span></span>;
}

export default function AppShell({children}:{children:React.ReactNode}){
 const router=useRouter(),pathname=usePathname(),[profile,setProfile]=useState<Profile|null>(null),[branding,setBranding]=useState<Branding|null>(null),[admin,setAdmin]=useState(false),[permissions,setPermissions]=useState<string[]|null>(null),[access,setAccess]=useState<Access|null>(null),[menuOpen,setMenuOpen]=useState(false),[openGroups,setOpenGroups]=useState<string[]>([]);
 useEffect(()=>{const session=getSession();if(!session){router.replace('/login');return}const load=()=>Promise.all([api<Profile>('/profile'),api<Branding>('/settings'),api<Access>('/subscription/access'),api<PermissionAccess>('/departments/my-access')]).then(([person,company,subscription,permissionAccess])=>{setProfile(person);setBranding(company);setAccess(subscription);setAdmin(permissionAccess.isAdministrator);setPermissions(permissionAccess.permissions)}).catch(()=>{});void load();window.addEventListener('profile-updated',load);window.addEventListener('branding-updated',load);return()=>{window.removeEventListener('profile-updated',load);window.removeEventListener('branding-updated',load)}},[router]);
 useEffect(()=>{if(!branding?.businessName)return;const page=groups.flatMap(group=>group.items).find(item=>pathname.startsWith(item.href))?.label;document.title=`${branding.businessName}${page?` — ${page}`:''}`;let icon=document.querySelector<HTMLLinkElement>('link[data-tenant-icon]');if(branding.logoUrl){if(!icon){icon=document.createElement('link');icon.rel='icon';icon.dataset.tenantIcon='true';document.head.appendChild(icon)}icon.href=branding.logoUrl}else icon?.remove();return()=>{document.title='LankaSaaS';document.querySelector<HTMLLinkElement>('link[data-tenant-icon]')?.remove()}},[branding,pathname]);
 useEffect(()=>{const current=groups.find(g=>g.items.some(i=>pathname.startsWith(i.href)));if(current)setOpenGroups(x=>x.includes(current.label)?x:[...x,current.label])},[pathname]);
 function toggle(label:string){setOpenGroups(x=>x.includes(label)?x.filter(v=>v!==label):[...x,label])}
 const can=(permission?:string)=>!permission||admin||permissions?.includes(permission)===true;
 const visibleGroups=groups.map(group=>({...group,items:group.items.filter(item=>can(item.permission))})).filter(group=>group.items.length>0);
 const currentItem=groups.flatMap(group=>group.items).find(item=>pathname.startsWith(item.href));
 const denied=permissions!==null&&currentItem&&!can(currentItem.permission);
 const name=profile?.firstName||'';
 return <div className="shell">
  {menuOpen&&<button className="nav-backdrop" aria-label="Close menu" onClick={()=>setMenuOpen(false)}/>}
  <aside className={`side ${menuOpen?'open':''}`}>
   <div className="brand-row"><Link className="tenant-brand" href="/dashboard">{branding?.logoUrl&&<img src={branding.logoUrl} alt={`${branding.businessName} logo`} onError={e=>{e.currentTarget.style.display='none'}}/>}<span>{branding?.businessName||<>Lanka<strong>SaaS</strong></>}</span></Link><button className="icon-btn mobile-only" aria-label="Close menu" onClick={()=>setMenuOpen(false)}>&times;</button></div>
   <nav className="nav" aria-label="Main navigation">{visibleGroups.map(group=>{const expanded=openGroups.includes(group.label);return <section className="nav-group" key={group.label}><button className="nav-group-button" aria-expanded={expanded} onClick={()=>toggle(group.label)}><span>{group.label}</span><span>{expanded?'−':'+'}</span></button>{expanded&&<div className="nav-items">{group.items.map(item=><Link key={item.href} href={item.href} className={pathname.startsWith(item.href)?'active':''} onClick={()=>setMenuOpen(false)}>{item.label}</Link>)}</div>}</section>})}</nav>
   <Link className="side-footer" href="/profile"><Avatar profile={profile}/><div><strong>{name||'Account'}</strong><small>{profile?.role==='Admin'?'Administrator':'Team member'}</small></div></Link>
  </aside>
  <main className="main"><header className="top"><button className="icon-btn mobile-only" aria-label="Open menu" onClick={()=>setMenuOpen(true)}>☰</button><div className="top-spacer"/><Link className="top-profile" href="/profile"><Avatar profile={profile} className="top-avatar"/><span>{name}</span></Link><button className="btn secondary compact" onClick={async()=>{await logout();router.push('/login')}}>Log out</button></header>{access&&(access.isReadOnly||access.status==='PastDue'||access.status==='Cancelled')&&<div className={`access-banner ${access.isReadOnly?'blocked':'warning'}`}><span>{access.message}</span>{admin&&<Link href="/subscription">Manage billing</Link>}</div>}<div className="content">{denied?<AccessDenied/>:children}</div></main>
 </div>;
}

function AccessDenied(){return <div className="empty access-denied"><span className="access-denied-icon" aria-hidden="true">!</span><h1>Access denied</h1><p className="muted">Your department does not have permission to open this area. Ask a company administrator if your responsibilities have changed.</p><Link className="btn" href="/dashboard">Return to dashboard</Link></div>}
