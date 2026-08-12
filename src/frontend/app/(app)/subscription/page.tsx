'use client';
import {useEffect,useState} from 'react';
import {api,getSession} from '@/lib/api';
import {useRouter} from 'next/navigation';

type Plan={code:string;name:string;monthlyPriceLkr:number;userLimit:number;description:string};
type Subscription={plan:string;status:string;userLimit:number;activeUsers:number;remainingUserSeats:number;trialEndsAt:string|null;subscriptionEndsAt:string|null;availablePlans:Plan[]};
const money=(value:number)=>new Intl.NumberFormat('en-LK',{style:'currency',currency:'LKR',maximumFractionDigits:0}).format(value);
const date=(value:string|null)=>value?new Date(value).toLocaleDateString():'Not set';

export default function Page(){
 const router=useRouter(),[data,setData]=useState<Subscription|null>(null),[error,setError]=useState('');
 useEffect(()=>{if(getSession()?.user.role!=='Admin'){router.replace('/dashboard');return}api<Subscription>('/subscription').then(setData).catch(e=>setError((e as Error).message))},[router]);
 if(!data)return <p className={error?'error':'muted'}>{error||'Loading subscription…'}</p>;
 const end=data.plan==='Trial'?data.trialEndsAt:data.subscriptionEndsAt;
 return <><div className="heading"><div><h1>Subscription</h1><p className="muted">Your plan controls how many active team members can access this business.</p></div></div>
 <div className="grid"><div className="card metric"><span className="muted">Current plan</span><strong>{data.plan}</strong></div><div className="card metric"><span className="muted">Status</span><strong>{data.status}</strong></div><div className="card metric"><span className="muted">Active users</span><strong>{data.activeUsers} / {data.userLimit}</strong></div><div className="card metric"><span className="muted">Remaining seats</span><strong>{data.remainingUserSeats}</strong></div></div>
 <div className="card" style={{marginTop:20}}><h2>{data.plan==='Trial'?'Trial ends':'Current period ends'}</h2><p>{date(end)}</p><p className="muted">Online plan changes and payments will be enabled when the payment provider is connected.</p></div>
 <h2 style={{marginTop:28}}>Available plans</h2><div className="grid" style={{gridTemplateColumns:'repeat(3,1fr)',marginTop:12}}>{data.availablePlans.map(plan=><div className="card" key={plan.code}><h2>{plan.name}</h2><p className="muted">{plan.description}</p><p><strong>{money(plan.monthlyPriceLkr)}</strong> / month</p><p>{plan.userLimit} active users</p></div>)}</div></>;
}
