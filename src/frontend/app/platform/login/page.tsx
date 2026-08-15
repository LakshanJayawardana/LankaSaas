'use client';
import {FormEvent,useState} from 'react';
import {useRouter} from 'next/navigation';
import {PlatformSession,platformApi,setPlatformSession} from '@/lib/platform-api';

export default function PlatformLogin(){
 const[email,setEmail]=useState(''),[password,setPassword]=useState(''),[error,setError]=useState(''),[busy,setBusy]=useState(false);const router=useRouter();
 async function submit(event:FormEvent){event.preventDefault();setBusy(true);setError('');try{const session=await platformApi<PlatformSession>('/auth/login',{method:'POST',body:JSON.stringify({email,password})});setPlatformSession(session);router.replace('/platform/tenants')}catch(x){setError((x as Error).message)}finally{setBusy(false)}}
 return <main className="platform-auth"><section className="platform-login-card"><div className="platform-mark">WW</div><p className="platform-eyebrow">WebWaves Digital</p><h1>Platform administration</h1><p className="muted">Sign in with your platform-owner account. Tenant administrator credentials do not work here.</p>{error&&<div className="error" role="alert">{error}</div>}<form onSubmit={submit}><label>Email<input type="email" autoComplete="username" required value={email} onChange={x=>setEmail(x.target.value)}/></label><label>Password<input type="password" autoComplete="current-password" required value={password} onChange={x=>setPassword(x.target.value)}/></label><button className="btn" disabled={busy}>{busy?'Signing in...':'Sign in to platform'}</button></form><p className="platform-security-note">Restricted operator access · All subscription changes are audited</p></section></main>
}
