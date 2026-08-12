'use client';
import Link from 'next/link';
import {useEffect,useState} from 'react';
import {api,getSession,logout} from '@/lib/api';
import {useRouter} from 'next/navigation';

type Access={status:string;isReadOnly:boolean;message:string;accessEndsAt:string|null};
export default function AppShell({children}:{children:React.ReactNode}){
 const router=useRouter(),[name,setName]=useState(''),[admin,setAdmin]=useState(false),[access,setAccess]=useState<Access|null>(null);
 useEffect(()=>{const s=getSession();if(!s)router.replace('/login');else{setName(s.user.firstName);setAdmin(s.user.role==='Admin');api<Access>('/subscription/access').then(setAccess).catch(()=>{})}},[router]);
 return <div className="shell"><aside className="side"><div className="brand">LankaSaaS</div><nav className="nav"><Link href="/dashboard">Dashboard</Link><Link href="/events">Events</Link><Link href="/event-finance">Event finance</Link><Link href="/logistics">Logistics</Link><Link href="/purchasing">Purchasing</Link><Link href="/accounting">Accounting</Link><Link href="/customers">Customers</Link><Link href="/products">Products</Link><Link href="/invoices">Invoices</Link><Link href="/expenses">Expenses</Link>{admin&&<><Link href="/team">Team</Link><Link href="/subscription">Subscription</Link><Link href="/settings">Settings</Link></>}</nav></aside><main className="main"><header className="top"><span>{name}</span><button className="btn secondary" onClick={async()=>{await logout();router.push('/login')}}>Log out</button></header>{access&&(access.isReadOnly||access.status==='PastDue'||access.status==='Cancelled')&&<div className={`access-banner ${access.isReadOnly?'blocked':'warning'}`}><span>{access.message}</span>{admin&&<Link href="/subscription">Manage billing</Link>}</div>}<div className="content">{children}</div></main></div>;
}
