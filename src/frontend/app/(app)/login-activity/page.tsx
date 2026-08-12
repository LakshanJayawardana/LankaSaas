'use client';
import {useEffect,useState} from 'react';
import {api,getSession} from '@/lib/api';
import {useRouter} from 'next/navigation';

type ActivityUser={userId:string;name:string;email:string;totalLogins:number;lastLoginAt:string|null};
type RecentLogin={id:string;userId:string;name:string;email:string;loggedInAt:string};
type Activity={totalLogins:number;loginsLast30Days:number;activeUsersLast30Days:number;users:ActivityUser[];recentLogins:RecentLogin[]};
const when=(value:string|null)=>value?new Date(value).toLocaleString():'Never';

export default function Page(){
 const router=useRouter(),[data,setData]=useState<Activity|null>(null),[error,setError]=useState('');
 useEffect(()=>{if(getSession()?.user.role!=='Admin'){router.replace('/dashboard');return}api<Activity>('/login-activity').then(setData).catch(e=>setError((e as Error).message))},[router]);
 return <><div className="heading"><div><h1>Login activity</h1><p className="muted">Successful sign-ins for your team. Detailed activity is retained for 90 days.</p></div></div>
 {error&&<p className="error">{error}</p>}{!data&&!error&&<p className="muted">Loading activity…</p>}{data&&<>
 <div className="grid"><div className="card metric"><span className="muted">Total logins</span><strong>{data.totalLogins}</strong></div><div className="card metric"><span className="muted">Last 30 days</span><strong>{data.loginsLast30Days}</strong></div><div className="card metric"><span className="muted">Active users (30 days)</span><strong>{data.activeUsersLast30Days}</strong></div></div>
 <h2 className="section-title">Team overview</h2><table className="table"><thead><tr><th>User</th><th>Email</th><th>Total logins</th><th>Last login</th></tr></thead><tbody>{data.users.map(u=><tr key={u.userId}><td>{u.name}</td><td>{u.email}</td><td>{u.totalLogins}</td><td>{when(u.lastLoginAt)}</td></tr>)}</tbody></table>
 <h2 className="section-title">Recent successful logins</h2><table className="table"><thead><tr><th>User</th><th>Email</th><th>Time</th></tr></thead><tbody>{data.recentLogins.length?data.recentLogins.map(x=><tr key={x.id}><td>{x.name}</td><td>{x.email}</td><td>{when(x.loggedInAt)}</td></tr>):<tr><td colSpan={3} className="muted">No login activity yet.</td></tr>}</tbody></table></>}</>;
}
