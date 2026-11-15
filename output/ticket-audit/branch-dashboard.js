
const S={base:'',token:'',branches:[],users:[],ticketTypes:[],claims:{}};
const $=id=>document.getElementById(id);
const esc=v=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
const unwrap=x=>x&&typeof x==='object'&&'data'in x?x.data:x;
function base(){return $('apiBase').value.trim().replace(/\/+$/,'')}
function showTab(id){['list','create','detail','consoleTab'].forEach(x=>$(x).classList.toggle('hidden',x!==id))}
function setStatus(code,ms){$('lastStatus').textContent=code;$('latency').textContent=ms+'ms'}
function log(obj,bad=false){$('console').textContent=JSON.stringify(obj,null,2);$('console').style.borderColor=bad?'#ef4444':'#29405f'}
function clearConsole(){$('console').textContent='Ready.'}
function parseJwt(t){try{return JSON.parse(atob(t.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')))}catch{return {}}}
async function request(path,opt={}){
  S.base=base(); const started=performance.now();
  const headers={Accept:'application/json',...(opt.body?{'Content-Type':'application/json'}:{}),...(opt.headers||{})};
  if(S.token)headers.Authorization='Bearer '+S.token;
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
    const res=await fetch(base()+'/api/Auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({companyCode:$('companyCode').value,userName:$('username').value,password:$('password').value})});
    const j=await res.json(); if(!res.ok)throw new Error(j.message||'Login failed');
    S.token=(j.data||j).accessToken; if(!S.token)throw new Error('No access token returned');
    S.claims=parseJwt(S.token); fillClaims();
    sessionStorage.setItem('btBase',base()); sessionStorage.setItem('btToken',S.token);
    $('authStatus').innerHTML='<span class="ok">Connected tenant token.</span> '+esc(S.claims.companyCode||'');
    await loadBranches();
  }catch(e){$('authStatus').innerHTML='<span class="badTxt">'+esc(e.message)+'</span>'}
}
function useToken(){S.base=base();S.token=$('manualToken').value.trim().replace(/^Bearer\s+/i,'');S.claims=parseJwt(S.token);fillClaims();sessionStorage.setItem('btBase',S.base);sessionStorage.setItem('btToken',S.token);$('authStatus').innerHTML='<span class="ok">Token loaded.</span>'}
function fillClaims(){
  const c=S.claims||{};
  const admin=c.isAdmin==='true';
  $('statusAction').disabled=!admin;
  $('newStatusId').disabled=!admin;
  $('isInternal').disabled=!admin;
  if(!admin)$('isInternal').value='false';
  if(c.companyId&&!$('companyId').value)$('companyId').value=c.companyId;
  if(c.branchId&&!$('createBranchId').value)$('createBranchId').value=c.branchId;
  if(c.userId&&!$('requesterId').value)$('requesterId').value=c.userId;
}
function params(obj){const p=new URLSearchParams();Object.entries(obj).forEach(([k,v])=>{if(v!==''&&v!=null)p.set(k,v)});return p.toString()}
async function loadBranches(){
  const branches=await request('/api/branches'); S.branches=Array.isArray(branches)?branches:[];
  $('branchCount').textContent=S.branches.length;
  $('branchSelect').innerHTML='<option value="">All visible branches</option>'+S.branches.map(b=>`<option value="${esc(b.branchId)}">${esc(b.branchNameEn||b.branchNameAr||'Branch')} · #${esc(b.branchId)}</option>`).join('');
  const first=S.branches[0]; if(first){$('branchSelect').value=first.branchId;$('createBranchId').value=first.branchId;$('companyId').value=first.companyId||$('companyId').value}
  try{S.users=await request('/api/users')}catch{}
}
async function loadTicketTypes(){S.ticketTypes=await request('/api/ticket-types'); $('detailBox').textContent=JSON.stringify(S.ticketTypes,null,2); showTab('detail')}
async function loadTickets(){
  const q=params({page:1,pageSize:$('pageSize').value,branchId:$('branchSelect').value,searchTerm:$('searchTerm').value,statusId:$('statusId').value,priorityId:$('priorityId').value,slaStatus:$('slaStatus').value,sortBy:'CreationDate',sortDirection:'DESC'});
  const data=await request('/api/tickets?'+q); const items=data.items||data.Items||[]; $('totalTickets').textContent=data.totalCount??items.length;
  $('ticketsBody').innerHTML=items.length?items.map(t=>`<tr onclick="selectTicket(${Number(t.ticketId||t.TicketId)})"><td>${esc(t.ticketId||t.TicketId)}</td><td>${esc(t.titleEn||t.TitleEn)}<br><span class="muted">${esc(t.description||t.Description)}</span></td><td>${esc(t.branchName||t.BranchName||t.branchId||t.BranchId)}</td><td>${esc(t.ticketStatusName||t.TicketStatusName||t.ticketStatusId||t.TicketStatusId)}</td><td>${esc(t.ticketPriorityName||t.TicketPriorityName||t.ticketPriorityId||t.TicketPriorityId)}</td><td>${esc(t.slaStatus||t.SlaStatus)}</td><td>${esc(t.requesterName||t.RequesterName||t.requesterId||t.RequesterId)}</td></tr>`).join(''):'<tr><td colspan="7" class="muted">No tickets returned.</td></tr>';
}
function selectTicket(id){$('ticketId').value=id;showTab('detail');getTicketDetail()}
async function createTicket(){
  const body={titleAr:$('titleAr').value,titleEn:$('titleEn').value,description:$('description').value,companyId:Number($('companyId').value),branchId:Number($('createBranchId').value),requesterId:Number($('requesterId').value),ticketTypeId:Number($('ticketTypeId').value),ticketPriorityId:Number($('ticketPriorityId').value),ticketCategoryId:$('ticketCategoryId').value?Number($('ticketCategoryId').value):null,attachments:[]};
  const id=await request('/api/tickets',{method:'POST',body:JSON.stringify(body)}); $('ticketId').value=id; showTab('detail'); await getTicketDetail();
}
async function getTicketDetail(){const id=$('ticketId').value.trim(); if(!id)return; $('detailBox').textContent=JSON.stringify(await request('/api/tickets/'+encodeURIComponent(id)),null,2)}
async function getComments(){const id=$('ticketId').value.trim(); if(!id)return; $('detailBox').textContent=JSON.stringify(await request('/api/tickets/'+encodeURIComponent(id)+'/comments'),null,2)}
async function updateStatus(){const id=$('ticketId').value.trim(); await request('/api/tickets/'+id+'/status',{method:'PUT',body:JSON.stringify({ticketId:Number(id),newStatusId:Number($('newStatusId').value),statusChangeReason:$('commentText').value})}); await getTicketDetail()}
async function addComment(){const id=$('ticketId').value.trim(); await request('/api/tickets/'+id+'/comments',{method:'POST',body:JSON.stringify({ticketId:Number(id),commentText:$('commentText').value,isInternal:$('isInternal').value==='true'})}); await getComments()}
addEventListener('DOMContentLoaded',()=>{const b=sessionStorage.getItem('btBase'),t=sessionStorage.getItem('btToken');if(b)$('apiBase').value=b;if(t){S.token=t;S.claims=parseJwt(t);fillClaims();$('authStatus').innerHTML='<span class="ok">Restored tenant token.</span>'}});
