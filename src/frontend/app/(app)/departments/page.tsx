'use client';

import {FormEvent,useEffect,useState} from 'react';
import {api} from '@/lib/api';

type Permission={permissionCode:string;minimumAccessLevel:string};
type Department={id:string;name:string;code:string;isSystem:boolean;isActive:boolean;permissions:Permission[]};
type AccessHistory={id:string;userId:string;actorName:string;description:string;createdAt:string};

const labels:Record<string,string>={
 'events.view':'View events','events.manage':'Create and edit events','events.change_status':'Change event status',
 'logistics.view':'View logistics','logistics.operate':'Reserve, dispatch and return resources','logistics.manage':'Manage logistics resources',
 'finance.view':'View event finance','finance.quotations':'Manage quotations','finance.payments':'Record customer payments','finance.manage':'Manage event finance',
 'accounting.view':'View accounting','accounting.post_journals':'Post accounting journals',
 'purchasing.view':'View purchasing','purchasing.operate':'Process purchase orders','purchasing.manage':'Manage suppliers and purchasing',
 'staffing.view':'View staffing','staffing.manage':'Manage event staffing','attendance.self':'Record own attendance','attendance.override':'Override attendance location checks',
 'contacts.view':'View customers','contacts.manage':'Manage customers',
 'administration.users':'Manage team and departments','administration.settings':'Manage company settings','administration.billing':'Manage subscription','administration.audit':'View audit and login activity'
};
const groups=[
 ['Events',['events.view','events.manage','events.change_status']],['Logistics',['logistics.view','logistics.operate','logistics.manage']],
 ['Finance',['finance.view','finance.quotations','finance.payments','finance.manage']],['Accounting',['accounting.view','accounting.post_journals']],
 ['Purchasing',['purchasing.view','purchasing.operate','purchasing.manage']],['Staff & attendance',['staffing.view','staffing.manage','attendance.self','attendance.override']],
 ['Contacts',['contacts.view','contacts.manage']],['Administration',['administration.users','administration.settings','administration.billing','administration.audit']]
] as const;

export default function Page(){
 const[departments,setDepartments]=useState<Department[]>([]),[catalogue,setCatalogue]=useState<string[]>([]),[history,setHistory]=useState<AccessHistory[]>([]),[editing,setEditing]=useState<Department|null>(null),[creating,setCreating]=useState(false),[deleting,setDeleting]=useState<Department|null>(null),[error,setError]=useState(''),[success,setSuccess]=useState('');
 async function load(){try{const[d,p,h]=await Promise.all([api<Department[]>('/departments'),api<string[]>('/departments/permissions'),api<AccessHistory[]>('/departments/access-history')]);setDepartments(d);setCatalogue(p);setHistory(h);setError('')}catch(e){setError((e as Error).message)}}
 useEffect(()=>{void load()},[]);
 async function save(e:FormEvent<HTMLFormElement>){e.preventDefault();const f=new FormData(e.currentTarget),permissions=catalogue.filter(code=>f.get(`permission:${code}`)==='on').map(permissionCode=>({permissionCode,minimumAccessLevel:String(f.get(`level:${permissionCode}`)||'Viewer')}));const body={name:f.get('name'),code:editing?.isSystem?editing.code:f.get('code'),isActive:f.get('isActive')==='on',permissions};try{if(editing)await api(`/departments/${editing.id}`,{method:'PUT',body:JSON.stringify(body)});else await api('/departments',{method:'POST',body:JSON.stringify(body)});setEditing(null);setCreating(false);await load();setSuccess(editing?'Department access updated.':'Department created.')}catch(x){setError((x as Error).message)}}
 async function remove(){if(!deleting)return;try{await api(`/departments/${deleting.id}`,{method:'DELETE'});setDeleting(null);await load();setSuccess('Department deleted.')}catch(x){setDeleting(null);setError((x as Error).message)}}
 const current=editing,showForm=creating||!!editing;
 return <>
  <div className="heading"><div><h1>Departments & access</h1><p className="muted">Control what each department can see and do.</p></div><button className="btn" onClick={()=>{setEditing(null);setCreating(true)}}>Add department</button></div>
  {error&&<p className="error">{error}</p>}
  <div className="department-grid">{departments.map(d=><article className={`card department-card ${d.isActive?'':'department-inactive'}`} key={d.id}><div className="department-card-head"><div><h2>{d.name}</h2><p className="muted">{d.code} · {d.isSystem?'Standard department':'Custom department'}</p></div><span className={`department-status ${d.isActive?'active':'inactive'}`}><i aria-hidden="true"/>{d.isActive?'Active':'Inactive'}</span></div><div className="permission-summary">{d.permissions.slice(0,4).map(p=><span key={p.permissionCode}>{labels[p.permissionCode]??p.permissionCode} · {p.minimumAccessLevel}+</span>)}{d.permissions.length>4&&<span>+{d.permissions.length-4} more permissions</span>}</div><div className="actions department-actions"><button className="btn secondary compact" onClick={()=>{setCreating(false);setEditing(d)}}>Configure</button>{!d.isSystem&&<button className="danger-link" onClick={()=>setDeleting(d)}>Delete</button>}</div></article>)}</div>
  <section className="card access-history"><div className="section-heading"><div><h2>Access change history</h2><p className="muted">Recent department and employee access changes.</p></div><span className="muted">Latest 100</span></div>{history.length?<div className="table-wrap"><table className="table"><thead><tr><th>Changed by</th><th>Change</th><th>Time</th></tr></thead><tbody>{history.map(item=><tr key={item.id}><td>{item.actorName}</td><td>{item.description}</td><td>{new Date(item.createdAt).toLocaleString()}</td></tr>)}</tbody></table></div>:<div className="empty compact-empty"><p className="muted">No access changes recorded yet.</p></div>}</section>
  {showForm&&<div className="dialog"><div className="card department-dialog"><div className="dialog-title"><div><h2>{current?'Configure department':'Add department'}</h2><p className="muted">Select a permission and the minimum access level required.</p></div><button className="icon-btn" aria-label="Close" onClick={()=>{setEditing(null);setCreating(false)}}>&times;</button></div><form onSubmit={save}><div className="two-fields"><label>Department name<input name="name" defaultValue={current?.name} maxLength={80} required/></label><label>Department code<input name="code" defaultValue={current?.code} disabled={current?.isSystem} pattern="[A-Za-z][A-Za-z0-9_-]{1,29}" required/></label></div><label className="check"><input name="isActive" type="checkbox" defaultChecked={current?.isActive??true}/> Department is active</label><div className="permission-groups">{groups.map(([group,codes])=><fieldset key={group}><legend>{group}</legend>{codes.filter(code=>catalogue.includes(code)).map(code=>{const assigned=current?.permissions.find(p=>p.permissionCode===code);return <div className="permission-row" key={code}><label className="check"><input type="checkbox" name={`permission:${code}`} defaultChecked={!!assigned}/><span>{labels[code]}</span></label><select name={`level:${code}`} defaultValue={assigned?.minimumAccessLevel??'Viewer'} aria-label={`Minimum access for ${labels[code]}`}><option value="Viewer">Viewer+</option><option value="Member">Member+</option><option value="Manager">Manager only</option></select></div>})}</fieldset>)}</div><div className="actions"><button type="button" className="btn secondary" onClick={()=>{setEditing(null);setCreating(false)}}>Cancel</button><button className="btn">Save department</button></div></form></div></div>}
  {deleting&&<div className="dialog"><div className="card confirm-card"><h2>Delete {deleting.name}?</h2><p>This is only possible when no team members belong to this department.</p><div className="actions"><button className="btn secondary" onClick={()=>setDeleting(null)}>Cancel</button><button className="btn danger" onClick={()=>void remove()}>Delete department</button></div></div></div>}
  {success&&<div className="dialog success-dialog"><div className="card"><div className="success-icon">&#10003;</div><h2>Success</h2><p>{success}</p><button className="btn" onClick={()=>setSuccess('')}>OK</button></div></div>}
 </>;
}
