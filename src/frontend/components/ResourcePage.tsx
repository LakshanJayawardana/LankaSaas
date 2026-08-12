'use client';
import {FormEvent,useEffect,useState} from 'react';
import {api} from '@/lib/api';

type Field={key:string;label:string;type?:string};
type Row=Record<string,string|number>;
export default function ResourcePage({title,path,fields}:{title:string;path:string;fields:Field[]}){
 const[rows,setRows]=useState<Row[]>([]),[open,setOpen]=useState(false),[error,setError]=useState('');
 async function load(){try{setRows(await api<Row[]>(path))}catch(e){setError((e as Error).message)}}
 useEffect(()=>{void load()},[]);
 async function submit(e:FormEvent<HTMLFormElement>){e.preventDefault();const raw:Record<string,FormDataEntryValue|number>=Object.fromEntries(new FormData(e.currentTarget));for(const f of fields.filter(x=>x.type==='number'))raw[f.key]=Number(raw[f.key]);try{await api(path,{method:'POST',body:JSON.stringify(raw)});setOpen(false);await load()}catch(x){setError((x as Error).message)}}
 const display=(r:Row,f:Field)=>((f.type==='number'&&f.key.toLowerCase().includes('price'))||f.key==='amount')?`LKR ${Number(r[f.key]).toFixed(2)}`:String(r[f.key]??'—');
 return <><div className="heading"><div><h1>{title}</h1><p className="muted">Manage your {title.toLowerCase()}.</p></div><button className="btn" onClick={()=>setOpen(true)}>Add {title.slice(0,-1)}</button></div>{error&&<p className="error">{error}</p>}<table className="table"><thead><tr>{fields.slice(0,4).map(f=><th key={f.key}>{f.label}</th>)}</tr></thead><tbody>{rows.length?rows.map((r,i)=><tr key={String(r.id??i)}>{fields.slice(0,4).map(f=><td key={f.key}>{display(r,f)}</td>)}</tr>):<tr><td colSpan={4} className="muted">No records yet.</td></tr>}</tbody></table>{open&&<div className="dialog"><div className="card"><h2>Add {title.slice(0,-1)}</h2><form onSubmit={submit}>{fields.map(f=><label key={f.key}>{f.label}<input name={f.key} type={f.type??'text'} required={['name','description','sku','amount','expenseDate','category'].includes(f.key)}/></label>)}<div className="actions"><button type="button" className="btn secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="btn">Save</button></div></form></div></div>}</>;
}
