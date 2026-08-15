'use client';
import {FormEvent,useEffect,useState} from 'react';
import {api,getSession} from '@/lib/api';
import {useRouter} from 'next/navigation';

type Plan={code:string;name:string;monthlyPriceLkr:number;userLimit:number;description:string};
type Billing={id:string;reference:string;plan:string;amount:number;currency:string;status:string;paymentMethod:string|null;createdAt:string};
type Subscription={plan:string;status:string;userLimit:number;activeUsers:number;remainingUserSeats:number;trialEndsAt:string|null;subscriptionEndsAt:string|null;cancellationRequestedAt:string|null;availablePlans:Plan[];billingHistory:Billing[]};
type Checkout={actionUrl:string;fields:Record<string,string>};
const money=(value:number)=>new Intl.NumberFormat('en-LK',{style:'currency',currency:'LKR',maximumFractionDigits:0}).format(value);
const date=(value:string|null)=>value?new Date(value).toLocaleDateString():'Not set';

export default function Page(){
 const router=useRouter(),[data,setData]=useState<Subscription|null>(null),[selected,setSelected]=useState<Plan|null>(null),[error,setError]=useState('');
 const load=()=>api<Subscription>('/subscription').then(setData).catch(e=>setError((e as Error).message));
 useEffect(()=>{if(getSession()?.user.role!=='Admin'){router.replace('/dashboard');return}void load()},[router]);
 if(!data)return <p className={error?'error':'muted'}>{error||'Loading subscription...'}</p>;
 const end=data.plan==='Trial'?data.trialEndsAt:data.subscriptionEndsAt;
 async function checkout(e:FormEvent<HTMLFormElement>){e.preventDefault();if(!selected)return;try{const values=Object.fromEntries(new FormData(e.currentTarget));const result=await api<Checkout>('/subscription/checkout',{method:'POST',body:JSON.stringify({...values,plan:selected.code})});const form=document.createElement('form');form.method='POST';form.action=result.actionUrl;Object.entries(result.fields).forEach(([name,value])=>{const input=document.createElement('input');input.type='hidden';input.name=name;input.value=value;form.appendChild(input)});document.body.appendChild(form);form.submit()}catch(x){setError((x as Error).message)}}
 async function cancel(){if(!confirm('Cancel recurring billing? Access remains available until the current period ends.'))return;try{await api('/subscription/cancel',{method:'POST'});setError('');await load()}catch(x){setError((x as Error).message)}}
 return <><div className="heading"><div><h1>Subscription</h1><p className="muted">Manage your plan, recurring billing, and payment history.</p></div></div>
 <div className="grid"><div className="card metric"><span className="muted">Current plan</span><strong>{data.plan}</strong></div><div className="card metric"><span className="muted">Status</span><strong>{data.status}</strong></div><div className="card metric"><span className="muted">Active users</span><strong>{data.activeUsers} / {data.userLimit}</strong></div><div className="card metric"><span className="muted">Remaining seats</span><strong>{data.remainingUserSeats}</strong></div></div>
 <div className="card" style={{marginTop:20}}><h2>{data.plan==='Trial'?'Trial ends':'Current period ends'}</h2><p>{date(end)}</p>{data.cancellationRequestedAt&&<p className="muted">Recurring billing cancelled on {date(data.cancellationRequestedAt)}.</p>}{data.status==='Active'&&<button className="danger-link" onClick={cancel}>Cancel subscription</button>}</div>
 {error&&<p className="error">{error}</p>}<h2 style={{marginTop:28}}>Available plans</h2><div className="grid subscription-plans">{data.availablePlans.map(plan=><div className="card" key={plan.code}><h2>{plan.name}</h2><p className="muted">{plan.description}</p><p><strong>{money(plan.monthlyPriceLkr)}</strong> / month</p><p>{plan.userLimit} active users</p><button className="btn" onClick={()=>setSelected(plan)}>Choose {plan.name}</button></div>)}</div>
 <h2 style={{marginTop:28}}>Billing history</h2><table className="table"><thead><tr><th>Date</th><th>Plan</th><th>Reference</th><th>Method</th><th>Status</th><th>Amount</th></tr></thead><tbody>{data.billingHistory.length?data.billingHistory.map(x=><tr key={x.id}><td>{new Date(x.createdAt).toLocaleString()}</td><td>{x.plan}</td><td>{x.reference}</td><td>{x.paymentMethod||'-'}</td><td>{x.status}</td><td>{money(x.amount)}</td></tr>):<tr><td colSpan={6} className="muted">No payments recorded yet.</td></tr>}</tbody></table>
 {selected&&<div className="dialog"><div className="card"><h2>Subscribe to {selected.name}</h2><p className="muted">You will continue securely on PayHere. LankaSaaS never receives your card details.</p><form onSubmit={checkout}><label>Phone<input name="phone" required maxLength={40}/></label><label>Billing address<input name="address" required maxLength={300}/></label><label>City<input name="city" required maxLength={80}/></label><div className="actions"><button type="button" className="btn secondary" onClick={()=>setSelected(null)}>Cancel</button><button className="btn">Continue to PayHere</button></div></form></div></div>}</>;
}
