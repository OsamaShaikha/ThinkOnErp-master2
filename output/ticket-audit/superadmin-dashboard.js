
const S={base:'',token:'',lastTickets:[]};
const $=id=>document.getElementById(id);
const esc=v=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
const unwrap=x=>x&&typeof x==='object'&&'data'in x?x.data:x;
function base(){return $('apiBase').value.trim().replace(/\/+$/,'')}
function setStatus(code,ms){$('lastStatus').textContent=code;$('latency').textContent=ms+'ms'}
function log(obj,bad=false){$('console').textContent=JSON.stringify(obj,null,2);$('console').style.borderColor=bad?'#ef4444':'#253a59'}
function clearConsole(){$('console').textContent='Ready.'}
function showTab(id){['list','detail','reports','mutate','consoleTab'].forEach(x=>$(x).classList.toggle('hidden',x!==id))}
async function request(path,opt={}){
  S.base=base(); const started=performance.now();
  const headers={Accept:'application/json',...(opt.body?{'Content-Type':'application/json'}:{}),...(opt.headers||{})};
  if(S.token)headers.Authorization='Bearer '+S.token;
  const selectedCompany=filterCompanyId.value.trim();
  if(!selectedCompany)throw new Error('Select a company ID before loading tickets.');
  headers['X-Company-Id']=selectedCompany;
  const res=await fetch(S.base+path,{...opt,headers});
  const text=await res.text(); let body=text; try{body=text?JSON.parse(text):null}catch{}
  const ms=Math.round(performance.now()-started); setStatus(res.status,ms);
  log({request:(opt.method||'GET')+' '+path,status:res.status,elapsedMs:ms,response:body},!res.ok);
  if(!res.ok)throw new Error(body?.message||body?.title||text||('HTTP '+res.status));
  return unwrap(body);
}
async function testHealth(){
  try{const r=await fetch(base()+'/api/AuditHealth/status'); const j=await r.json(); $('authStatus').innerHTML='<span class="ok">API healthy:</span> '+esc(j.status||r.status)}
  catch(e){$('authStatus').innerHTML='<span class="badTxt">API unreachable:</span> '+esc(e.message)}
}
async function login(){
  try{
    const r=await fetch(base()+'/api/auth/superadmin/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({userName:$('username').value,password:$('password').value})});
    const j=await r.json(); if(!r.ok)throw new Error(j.message||'Login failed');
    S.token=(j.data||j).accessToken; if(!S.token)throw new Error('No access token returned');
    sessionStorage.setItem('satBase',base()); sessionStorage.setItem('satToken',S.token);
    $('authStatus').innerHTML='<span class="ok">Connected as SuperAdmin.</span>';
    await loadTickets();
  }catch(e){$('authStatus').innerHTML='<span class="badTxt">'+esc(e.message)+'</span>'}
}
function useToken(){S.base=base();S.token=$('manualToken').value.trim().replace(/^Bearer\s+/i,'');sessionStorage.setItem('satBase',S.base);sessionStorage.setItem('satToken',S.token);$('authStatus').innerHTML='<span class="ok">Token loaded.</span>'}
function params(obj){const p=new URLSearchParams();Object.entries(obj).forEach(([k,v])=>{if(v!==''&&v!=null)p.set(k,v)});return p.toString()}
async function loadTickets(){
  const q=params({page:$('page').value,pageSize:$('pageSize').value,companyId:$('filterCompanyId').value,branchId:$('filterBranchId').value,searchTerm:$('filterSearch').value,statusId:$('filterStatusId').value,priorityId:$('filterPriorityId').value,slaStatus:$('filterSla').value,sortBy:$('sortBy').value,sortDirection:'DESC'});
  const data=await request('/api/tickets?'+q); const items=data.items||data.Items||[]; S.lastTickets=items;
  $('totalTickets').textContent=data.totalCount??items.length; $('pageInfo').textContent=(data.page||$('page').value)+'/'+(data.totalPages||'?');
  $('ticketsBody').innerHTML=items.length?items.map(t=>`<tr onclick="selectTicket(${Number(t.ticketId||t.TicketId)})"><td>${esc(t.ticketId||t.TicketId)}</td><td>${esc(t.titleEn||t.TitleEn)}<br><span class="muted">${esc(t.description||t.Description)}</span></td><td>${esc(t.companyName||t.CompanyName||t.companyId||t.CompanyId)}<br>${esc(t.branchName||t.BranchName||t.branchId||t.BranchId)}</td><td>${esc(t.ticketStatusName||t.TicketStatusName||t.ticketStatusId||t.TicketStatusId)}</td><td>${esc(t.ticketPriorityName||t.TicketPriorityName||t.ticketPriorityId||t.TicketPriorityId)}</td><td>${esc(t.slaStatus||t.SlaStatus)}</td><td>${esc(t.creationDate||t.CreationDate)}</td></tr>`).join(''):'<tr><td colspan="7" class="muted">No tickets returned.</td></tr>';
}
function selectTicket(id){$('detailId').value=id;$('actionTicketId').value=id;showTab('detail');getTicketDetail()}
async function loadTicketTypes(){const d=await request('/api/ticket-types'); $('detailBox').textContent=JSON.stringify(d,null,2); showTab('detail')}
async function getTicketDetail(){const id=$('detailId').value.trim(); if(!id)return; $('detailBox').textContent=JSON.stringify(await request('/api/tickets/'+encodeURIComponent(id)),null,2)}
async function getComments(){const id=$('detailId').value.trim(); if(!id)return; $('detailBox').textContent=JSON.stringify(await request('/api/tickets/'+encodeURIComponent(id)+'/comments'),null,2)}
async function getAttachments(){const id=$('detailId').value.trim(); if(!id)return; $('detailBox').textContent=JSON.stringify(await request('/api/tickets/'+encodeURIComponent(id)+'/attachments'),null,2)}
async function runReport(name){const q=params({startDate:$('startDate').value,endDate:$('endDate').value,groupBy:name==='volume' ? $('groupBy').value : ''}); $('reportBox').textContent=JSON.stringify(await request('/api/tickets/reports/'+name+'?'+q),null,2)}
async function assignTicket(){const id=$('actionTicketId').value.trim(); await request('/api/tickets/'+id+'/assign',{method:'PUT',body:JSON.stringify({ticketId:Number(id),assigneeId:$('assigneeId').value?Number($('assigneeId').value):null,assignmentReason:$('actionReason').value})})}
async function updateStatus(){const id=$('actionTicketId').value.trim(); await request('/api/tickets/'+id+'/status',{method:'PUT',body:JSON.stringify({ticketId:Number(id),newStatusId:Number($('newStatusId').value),statusChangeReason:$('actionReason').value})})}
async function addComment(internal){const id=$('actionTicketId').value.trim(); await request('/api/tickets/'+id+'/comments',{method:'POST',body:JSON.stringify({ticketId:Number(id),commentText:$('actionReason').value,isInternal:!!internal})})}
addEventListener('DOMContentLoaded',()=>{const b=sessionStorage.getItem('satBase'),t=sessionStorage.getItem('satToken');if(b)$('apiBase').value=b;if(t){S.token=t;$('authStatus').innerHTML='<span class="ok">Restored SuperAdmin token.</span>'}});
