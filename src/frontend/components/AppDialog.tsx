'use client';

import {FormEvent,useEffect,useState} from 'react';

type BaseOptions={title:string;message:string;confirmLabel?:string;cancelLabel?:string;danger?:boolean};
type ConfirmRequest={kind:'confirm';options:BaseOptions;resolve:(value:boolean)=>void};
type PromptOptions=BaseOptions&{label:string;defaultValue?:string;placeholder?:string;inputType?:'text'|'number';min?:number;step?:number};
type PromptRequest={kind:'prompt';options:PromptOptions;resolve:(value:string|null)=>void};
type Request=ConfirmRequest|PromptRequest;
let showDialog:((request:Request)=>void)|null=null;

export function appConfirm(options:BaseOptions){return new Promise<boolean>(resolve=>showDialog?.({kind:'confirm',options,resolve})??resolve(false))}
export function appPrompt(options:PromptOptions){return new Promise<string|null>(resolve=>showDialog?.({kind:'prompt',options,resolve})??resolve(null))}

export function AppDialogHost(){
 const[request,setRequest]=useState<Request|null>(null);
 useEffect(()=>{showDialog=setRequest;return()=>{showDialog=null}},[]);
 if(!request)return null;
 const close=(value:boolean|string|null)=>{if(request.kind==='confirm')request.resolve(Boolean(value));else request.resolve(typeof value==='string'?value:null);setRequest(null)};
 const options=request.options;
 function submit(e:FormEvent<HTMLFormElement>){e.preventDefault();if(request?.kind!=='prompt')return;const value=String(new FormData(e.currentTarget).get('value')||'').trim();if(value)close(value)}
 return <div className="dialog app-dialog" role="dialog" aria-modal="true" aria-labelledby="app-dialog-title" onMouseDown={e=>{if(e.target===e.currentTarget)close(request.kind==='confirm'?false:null)}}>
  <div className="card">
   <div className={`dialog-symbol ${options.danger?'danger':'info'}`} aria-hidden="true">{options.danger?'!':'?'}</div>
   <h2 id="app-dialog-title">{options.title}</h2>
   <p>{options.message}</p>
   {request.kind==='prompt'?<form onSubmit={submit}>
    <label>{request.options.label}<input name="value" type={request.options.inputType||'text'} min={request.options.min} step={request.options.step} defaultValue={request.options.defaultValue} placeholder={request.options.placeholder} autoFocus required/></label>
    <div className="actions"><button type="button" className="btn secondary" onClick={()=>close(null)}>{options.cancelLabel||'Cancel'}</button><button className={`btn ${options.danger?'danger':''}`}>{options.confirmLabel||'Continue'}</button></div>
   </form>:<div className="actions"><button className="btn secondary" autoFocus onClick={()=>close(false)}>{options.cancelLabel||'Cancel'}</button><button className={`btn ${options.danger?'danger':''}`} onClick={()=>close(true)}>{options.confirmLabel||'Confirm'}</button></div>}
  </div>
 </div>;
}
