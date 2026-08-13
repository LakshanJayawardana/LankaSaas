'use client';
import {FormEvent,useEffect,useState} from 'react';
import {api} from '@/lib/api';

type Profile={id:string;firstName:string;lastName:string;email:string;role:string;profilePhotoUrl?:string|null};

export default function Page(){
 const [profile,setProfile]=useState<Profile|null>(null),[photoUrl,setPhotoUrl]=useState(''),[message,setMessage]=useState(''),[error,setError]=useState('');
 useEffect(()=>{api<Profile>('/profile').then(x=>{setProfile(x);setPhotoUrl(x.profilePhotoUrl||'')}).catch(e=>setError(e.message))},[]);
 async function save(e:FormEvent){e.preventDefault();setMessage('');setError('');try{const updated=await api<Profile>('/profile',{method:'PUT',body:JSON.stringify({profilePhotoUrl:photoUrl||null})});setProfile(updated);window.dispatchEvent(new Event('profile-updated'));setMessage('Profile picture updated.')}catch(e){setError((e as Error).message)}}
 if(!profile)return <p className={error?'error':'muted'}>{error||'Loading profile…'}</p>;
 return <><div className="heading"><div><h1>My profile</h1><p className="muted">Your personal details within this company account.</p></div></div>{message&&<p className="success">{message}</p>}{error&&<p className="error">{error}</p>}<div className="profile-layout"><section className="card profile-summary"><span className="profile-photo">{photoUrl?<img src={photoUrl} alt={`${profile.firstName} ${profile.lastName}`} onError={e=>{e.currentTarget.style.display='none'}}/>:<span>{profile.firstName[0]}</span>}</span><div><h2>{profile.firstName} {profile.lastName}</h2><p className="muted">{profile.email}</p><span className="badge info">{profile.role}</span></div></section><form className="card" onSubmit={save}><h2>Profile picture</h2><p className="muted">Use a square HTTPS image. For best results, choose at least 256 × 256 pixels.</p><label>Profile picture HTTPS URL<input type="url" value={photoUrl} onChange={e=>setPhotoUrl(e.target.value)} placeholder="https://example.com/my-photo.jpg" maxLength={500}/></label>{photoUrl&&<button type="button" className="link-button remove-photo" onClick={()=>setPhotoUrl('')}>Remove picture</button>}<div className="actions"><button className="btn">Save profile</button></div></form></div></>;
}
