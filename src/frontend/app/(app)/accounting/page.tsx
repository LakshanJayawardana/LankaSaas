'use client';

import {FormEvent,useEffect,useMemo,useState} from 'react';
import {appConfirm} from '@/components/AppDialog';
import {api} from '@/lib/api';
import {money} from '@/lib/invoices';

type Account={id:string;code:string;name:string;type:string;balance:number};
type Line={accountCode:string;accountName:string;debit:number;credit:number};
type Journal={id:string;entryDate:string;description:string;reference?:string;sourceType:string;lines:Line[]};
type PL={revenue:number;expenses:number;netProfit:number;from:string;to:string};
type ImportRow={accountCode:string;accountName:string;accountType:string;debit:number;credit:number};
type ImportResult={accountsCreated:number;linesImported:number;totalDebit:number};

const accountTypes=['Asset','Liability','Equity','Revenue','Expense'];
export default function Page(){
 const[accounts,setAccounts]=useState<Account[]>([]),[journals,setJournals]=useState<Journal[]>([]),[pl,setPl]=useState<PL|null>(null),[error,setError]=useState(''),[success,setSuccess]=useState('');
 const[rows,setRows]=useState<ImportRow[]>([]),[fileName,setFileName]=useState(''),[importing,setImporting]=useState(false);
 const totals=useMemo(()=>({debit:rows.reduce((s,x)=>s+x.debit,0),credit:rows.reduce((s,x)=>s+x.credit,0)}),[rows]);
 const load=()=>Promise.all([api<Account[]>('/accounting/accounts'),api<Journal[]>('/accounting/journals'),api<PL>('/accounting/profit-loss')]).then(([a,j,p])=>{setAccounts(a);setJournals(j);setPl(p)}).catch(e=>setError(e.message));
 useEffect(()=>{void load()},[]);

 async function manual(e:FormEvent<HTMLFormElement>){e.preventDefault();const form=e.currentTarget;const f=Object.fromEntries(new FormData(form)),amount=+f.amount;setError('');setSuccess('');try{await api('/accounting/journals',{method:'POST',body:JSON.stringify({entryDate:f.entryDate,description:f.description,reference:f.reference||null,eventId:null,lines:[{accountId:f.debitAccountId,debit:amount,credit:0},{accountId:f.creditAccountId,debit:0,credit:amount}]})});form.reset();setSuccess('Journal posted successfully.');await load()}catch(x){setError((x as Error).message)}}
 async function selectFile(file?:File){setRows([]);setFileName('');setError('');setSuccess('');if(!file)return;try{const parsed=parseCsv(await file.text());setRows(parsed);setFileName(file.name)}catch(e){setError((e as Error).message)}}
 async function importBalances(e:FormEvent<HTMLFormElement>){e.preventDefault();if(!rows.length)return;const form=e.currentTarget,f=Object.fromEntries(new FormData(form));if(totals.debit!==totals.credit){setError('Debit and credit totals must be equal before importing.');return}const confirmed=await appConfirm({title:'Post opening balances?',message:`This will create one permanent opening journal for ${money(totals.debit)} from ${fileName}. Review the preview carefully before continuing.`,confirmLabel:'Post balances'});if(!confirmed)return;setImporting(true);setError('');setSuccess('');try{const result=await api<ImportResult>('/accounting/migration/opening-balances',{method:'POST',body:JSON.stringify({openingDate:f.openingDate,importReference:f.importReference,lines:rows})});setSuccess(`Opening balances imported: ${result.linesImported} rows and ${result.accountsCreated} new accounts.`);setRows([]);setFileName('');form.reset();await load()}catch(x){setError((x as Error).message)}finally{setImporting(false)}}

 return <>
  <div className="heading"><div><h1>Accounting</h1><p className="muted">Balanced journals, account balances and event profit in LKR.</p></div></div>
  {error&&<p className="error" role="alert">{error}</p>}{success&&<p className="success" role="status">{success}</p>}
  {pl&&<div className="grid"><Metric label="Revenue" value={pl.revenue}/><Metric label="Expenses" value={pl.expenses}/><Metric label="Net profit" value={pl.netProfit}/></div>}
  <section className="card accounting-import">
   <div className="heading"><div><h2>Move accounting data</h2><p className="muted">Import a reviewed opening trial balance from Excel without bringing unreliable historical transactions into the new system.</p></div><a className="btn secondary" href="/downloads/accounting-opening-balances-sample.csv" download>Download compatible sample CSV</a></div>
   <div className="import-steps"><span><strong>1</strong> Download and complete</span><span><strong>2</strong> Upload and review</span><span><strong>3</strong> Confirm balanced journal</span></div>
   <label className="file-drop">Opening balance CSV<input type="file" accept=".csv,text/csv" onChange={e=>void selectFile(e.target.files?.[0])}/><small>{fileName||'Choose the completed CSV exported from Excel'}</small></label>
   {rows.length>0&&<form onSubmit={importBalances}>
    <div className="migration-fields"><label>Opening date<input name="openingDate" type="date" required/></label><label>Unique import reference<input name="importReference" placeholder="e.g. OPENING-2026-08" maxLength={100} required/></label></div>
    <div className="table-wrap migration-preview"><table className="table"><thead><tr><th>Code</th><th>Account</th><th>Type</th><th>Debit</th><th>Credit</th></tr></thead><tbody>{rows.map((x,i)=><tr key={`${x.accountCode}-${i}`}><td>{x.accountCode}</td><td>{x.accountName}</td><td>{x.accountType}</td><td>{x.debit?money(x.debit):'—'}</td><td>{x.credit?money(x.credit):'—'}</td></tr>)}</tbody><tfoot><tr><th colSpan={3}>{rows.length} rows</th><th>{money(totals.debit)}</th><th>{money(totals.credit)}</th></tr></tfoot></table></div>
    <div className={`balance-check ${totals.debit===totals.credit?'valid':'invalid'}`}>{totals.debit===totals.credit?'Ready to import: debits and credits balance.':'Cannot import: debit and credit totals do not balance.'}</div>
    <div className="actions"><button type="button" className="btn secondary" onClick={()=>{setRows([]);setFileName('')}}>Clear preview</button><button className="btn" disabled={importing||totals.debit!==totals.credit}>{importing?'Importing…':'Import opening balances'}</button></div>
   </form>}
  </section>
  <div className="logistics-grid" style={{marginTop:20}}><div className="card"><h2>Chart of accounts</h2><div className="table-wrap"><table className="table"><thead><tr><th>Code</th><th>Account</th><th>Balance</th></tr></thead><tbody>{accounts.map(x=><tr key={x.id}><td>{x.code}</td><td>{x.name}<br/><span className="muted">{x.type}</span></td><td>{money(x.balance)}</td></tr>)}</tbody></table></div></div><div className="card"><h2>Manual journal</h2><form onSubmit={manual}><input name="entryDate" type="date" required/><input name="description" placeholder="Description" required/><input name="reference" placeholder="Reference"/><select name="debitAccountId" required defaultValue=""><option value="" disabled>Debit account</option>{accounts.map(x=><option key={x.id} value={x.id}>{x.code} — {x.name}</option>)}</select><select name="creditAccountId" required defaultValue=""><option value="" disabled>Credit account</option>{accounts.map(x=><option key={x.id} value={x.id}>{x.code} — {x.name}</option>)}</select><input name="amount" type="number" min="0.01" step="0.01" placeholder="Amount (LKR)" required/><button className="btn">Post journal</button></form></div></div>
  <h2 style={{marginTop:28}}>Recent journals</h2><div className="table-wrap"><table className="table"><thead><tr><th>Date</th><th>Description</th><th>Source</th><th>Debit</th><th>Credit</th></tr></thead><tbody>{journals.map(j=><tr key={j.id}><td>{j.entryDate}</td><td>{j.description}{j.reference&&<><br/><small>{j.reference}</small></>}</td><td>{j.sourceType==='OpeningBalanceImport'?'Opening import':j.sourceType}</td><td>{money(j.lines.reduce((s,x)=>s+x.debit,0))}</td><td>{money(j.lines.reduce((s,x)=>s+x.credit,0))}</td></tr>)}</tbody></table></div>
 </>;
}

function parseCsv(text:string){
 const lines=text.replace(/^\uFEFF/,'').split(/\r?\n/).filter(x=>x.trim());if(lines.length<3)throw new Error('The CSV must contain a header and at least two account rows.');
 const values=lines.map(parseCsvLine),headers=values[0].map(x=>x.trim().toLowerCase()),expected=['account code','account name','account type','debit','credit'];if(expected.some((x,i)=>headers[i]!==x))throw new Error('Use the downloaded template without changing its five column headings.');
 const seen=new Set<string>();return values.slice(1).map((x,index)=>{if(x.length!==5)throw new Error(`Row ${index+2} must contain exactly five columns.`);const accountCode=x[0].trim(),accountName=x[1].trim(),accountType=x[2].trim(),debit=number(x[3],index),credit=number(x[4],index);if(!accountCode||!accountName)throw new Error(`Row ${index+2} needs an account code and name.`);if(seen.has(accountCode.toLowerCase()))throw new Error(`Account code ${accountCode} appears more than once.`);seen.add(accountCode.toLowerCase());if(!accountTypes.includes(accountType))throw new Error(`Row ${index+2} has an invalid account type.`);if((debit>0)===(credit>0))throw new Error(`Row ${index+2} must contain either a debit or credit amount.`);return{accountCode,accountName,accountType,debit,credit}});
}
function parseCsvLine(line:string){const result:string[]=[];let value='',quoted=false;for(let i=0;i<line.length;i++){const c=line[i];if(c==='"'){if(quoted&&line[i+1]==='"'){value+='"';i++}else quoted=!quoted}else if(c===','&&!quoted){result.push(value);value=''}else value+=c}result.push(value);return result}
function number(value:string,row:number){const clean=value.trim().replace(/,/g,'');if(!clean)return 0;const parsed=Number(clean);if(!Number.isFinite(parsed)||parsed<0)throw new Error(`Row ${row+2} contains an invalid amount.`);return Math.round(parsed*100)/100}
function Metric({label,value}:{label:string,value:number}){return <div className="card metric"><span className="muted">{label}</span><strong>{money(value)}</strong></div>}
