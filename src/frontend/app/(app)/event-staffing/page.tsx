'use client';
import {FormEvent,useEffect,useState} from 'react';
import {api,getSession} from '@/lib/api';
import {money} from '@/lib/invoices';
import styles from './attendance.module.css';

type Event={id:string;name:string;startsAt:string};
type Person={id:string;name:string;role:string};
type Assignment={id:string;userId:string;staffName:string;responsibility:string;shiftStartsAt:string;shiftEndsAt:string;plannedHours:number;plannedCost:number;status:string;actualHours:number;actualCost:number};
type Policy={requireLocation:boolean;radiusMeters:number;maximumAccuracyMeters:number;checkInWindowMinutes:number};
type Staffing={eventName:string;plannedLabourCost:number;actualLabourCost:number;attendancePolicy:Policy;assignments:Assignment[]};
type Position={latitude:number;longitude:number;accuracyMeters:number};

function position():Promise<Position>{return new Promise((resolve,reject)=>{if(!navigator.geolocation){reject(new Error('This device does not support location services.'));return}navigator.geolocation.getCurrentPosition(p=>resolve({latitude:p.coords.latitude,longitude:p.coords.longitude,accuracyMeters:p.coords.accuracy}),()=>reject(new Error('Location is required. Enable location permission, move to an open area and try again.')),{enableHighAccuracy:true,timeout:15000,maximumAge:0})})}

export default function Page(){
 const session=getSession(),isAdmin=session?.user.role==='Admin',currentUserId=session?.user.id;
 const[events,setEvents]=useState<Event[]>([]),[people,setPeople]=useState<Person[]>([]),[eventId,setEventId]=useState(''),[data,setData]=useState<Staffing|null>(null),[error,setError]=useState(''),[workingId,setWorkingId]=useState('');
 useEffect(()=>{Promise.all([api<Event[]>('/events'),isAdmin?api<Person[]>('/staffing/team'):Promise.resolve([])]).then(([e,p])=>{setEvents(e);setPeople(p)}).catch(e=>setError(e.message))},[isAdmin]);
 async function load(id:string){setEventId(id);setError('');setData(id?await api<Staffing>(`/events/${id}/staffing`):null)}
 async function assign(e:FormEvent<HTMLFormElement>){e.preventDefault();const f=Object.fromEntries(new FormData(e.currentTarget));try{await api(`/events/${eventId}/staffing`,{method:'POST',body:JSON.stringify({userId:f.userId,responsibility:f.responsibility,shiftStartsAt:new Date(String(f.shiftStartsAt)).toISOString(),shiftEndsAt:new Date(String(f.shiftEndsAt)).toISOString(),hourlyRate:+f.hourlyRate,notes:f.notes||null})});e.currentTarget.reset();await load(eventId)}catch(x){setError((x as Error).message)}}
 async function action(x:Assignment,kind:'check-in'|'check-out'){
  setError('');setWorkingId(x.id);
  try{
   const otherUser=x.userId!==currentUserId;
   if(otherUser){if(!isAdmin)throw new Error('You can record attendance only for your own assignment.');const reason=window.prompt(`Supervisor override reason for ${x.staffName}:`);if(!reason?.trim())return;await api(`/events/${eventId}/staffing/${x.id}/${kind}`,{method:'POST',body:JSON.stringify({isOverride:true,overrideReason:reason.trim()})})}
   else{const coords=data?.attendancePolicy.requireLocation?await position():{};await api(`/events/${eventId}/staffing/${x.id}/${kind}`,{method:'POST',body:JSON.stringify(coords)})}
   await load(eventId);
  }catch(e){setError((e as Error).message)}finally{setWorkingId('')}
 }
 async function cancel(x:Assignment){try{await api(`/events/${eventId}/staffing/${x.id}/cancel`,{method:'PATCH'});await load(eventId)}catch(e){setError((e as Error).message)}}
 return <><div className="heading"><div><h1>Event staffing</h1><p className="muted">Schedule teams, verify on-site attendance and include labour in event costs.</p></div></div>{error&&<p className="error">{error}</p>}<label>Select event<select value={eventId} onChange={e=>void load(e.target.value)}><option value="">Choose event</option>{events.map(x=><option key={x.id} value={x.id}>{x.name} - {new Date(x.startsAt).toLocaleDateString()}</option>)}</select></label>{data&&<>{data.attendancePolicy.requireLocation?<p className={styles.policy}>Location verification is required within <strong>{data.attendancePolicy.radiusMeters} metres</strong>. GPS accuracy must be within <strong>{data.attendancePolicy.maximumAccuracyMeters} metres</strong>.</p>:<p className="muted">This event does not require location verification for attendance.</p>}<div className="grid" style={{marginTop:20}}><Metric label="Planned labour" value={data.plannedLabourCost}/><Metric label="Actual labour" value={data.actualLabourCost}/></div>{isAdmin&&<div className="card" style={{marginTop:20}}><h2>Assign team member</h2><form className="form-grid" onSubmit={assign}><label>Team member<select name="userId" required defaultValue=""><option value="" disabled>Select member</option>{people.map(x=><option key={x.id} value={x.id}>{x.name} - {x.role}</option>)}</select></label><label>Responsibility<input name="responsibility" placeholder="Coordinator, driver, setup" required/></label><label>Shift starts<input name="shiftStartsAt" type="datetime-local" required/></label><label>Shift ends<input name="shiftEndsAt" type="datetime-local" required/></label><label>Hourly rate (LKR)<input name="hourlyRate" type="number" min="0" step="0.01" required/></label><label>Notes<input name="notes"/></label><button className="btn">Assign</button></form></div>}<h2 style={{marginTop:28}}>{isAdmin?'Staff schedule':'My schedule'}</h2><table className="table"><thead><tr><th>Team member</th><th>Responsibility</th><th>Shift</th><th>Planned</th><th>Attendance</th><th>Actual cost</th><th>Actions</th></tr></thead><tbody>{data.assignments.map(x=><tr key={x.id}><td>{x.staffName}</td><td>{x.responsibility}</td><td>{new Date(x.shiftStartsAt).toLocaleString()}<br/>{new Date(x.shiftEndsAt).toLocaleString()}</td><td>{x.plannedHours}h · {money(x.plannedCost)}</td><td>{x.status}{x.actualHours>0&&` · ${x.actualHours}h`}</td><td>{money(x.actualCost)}</td><td>{x.status==='Scheduled'&&<><button className="link-button" disabled={workingId===x.id} onClick={()=>void action(x,'check-in')}>{workingId===x.id?'Checking location…':x.userId===currentUserId?'Check in':'Supervisor check-in'}</button>{isAdmin&&<button className="link-button" onClick={()=>void cancel(x)}>Cancel</button>}</>}{x.status==='CheckedIn'&&<button className="link-button" disabled={workingId===x.id} onClick={()=>void action(x,'check-out')}>{workingId===x.id?'Checking location…':x.userId===currentUserId?'Check out':'Supervisor check-out'}</button>}</td></tr>)}</tbody></table></>}</>;
}
function Metric({label,value}:{label:string,value:number}){return <div className="card metric"><span className="muted">{label}</span><strong>{money(value)}</strong></div>}
