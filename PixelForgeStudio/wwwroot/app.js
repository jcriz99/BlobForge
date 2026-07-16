const $=s=>document.querySelector(s), $$=s=>[...document.querySelectorAll(s)];
const api=async(url,opts={})=>{const r=await fetch(url,{headers:{'Content-Type':'application/json',...(opts.headers||{})},...opts});if(!r.ok){let e=await r.json().catch(()=>({error:r.statusText}));throw Error(e.error||r.statusText)}return r.headers.get('content-type')?.includes('json')?r.json():r};
const projectCategories=['Tilesets','Objects','Misc Art'];
let project=null,projectList=[],layerIndex=0,frameIndex=0,colorIndex=0,tool='pencil',zoom=16,showGrid=true,dirty=false,saving=false,playing=false,playTimer=null;
let drawing=false,startPoint=null,lastPoint=null,undoStack=[],redoStack=[],saveTimer=null;
const canvas=$('#canvas'),ctx=canvas.getContext('2d',{alpha:true});

function toast(message){let t=$('#toast');t.textContent=message;t.classList.add('show');clearTimeout(t._timer);t._timer=setTimeout(()=>t.classList.remove('show'),1800)}
function cloneProject(){return JSON.parse(JSON.stringify(project))}
function checkpoint(){if(!project)return;undoStack.push(cloneProject());if(undoStack.length>40)undoStack.shift();redoStack=[];updateUndo()}
function undo(){if(!undoStack.length)return;redoStack.push(cloneProject());project=undoStack.pop();normalizeSelection();changed()}
function redo(){if(!redoStack.length)return;undoStack.push(cloneProject());project=redoStack.pop();normalizeSelection();changed()}
function updateUndo(){$('#undo').disabled=!undoStack.length;$('#redo').disabled=!redoStack.length}
function normalizeSelection(){frameIndex=Math.min(frameIndex,project.frameDurationsMs.length-1);layerIndex=Math.min(layerIndex,project.layers.length-1);colorIndex=Math.min(colorIndex,project.palette.length-1)}

function renderProjectOptions(preferredName){
  const sel=$('#projectSelect'),query=$('#projectSearch').value.trim().toLowerCase();
  const matches=projectList.filter(p=>!query||p.name.toLowerCase().includes(query)||p.category.toLowerCase().includes(query));
  sel.innerHTML='';
  if(query&&project&&!matches.some(p=>p.name===project.name)){
    const current=projectList.find(p=>p.name===project.name);
    if(current){const group=document.createElement('optgroup');group.label='Currently Open';group.append(projectOption(current));sel.append(group)}
  }
  projectCategories.forEach(category=>{
    const items=matches.filter(p=>p.category===category).sort((a,b)=>a.name.localeCompare(b.name));
    if(!items.length)return;
    const group=document.createElement('optgroup');group.label=`${category} (${items.length})`;
    items.forEach(p=>group.append(projectOption(p)));sel.append(group)
  });
  if(!sel.options.length){const option=document.createElement('option');option.disabled=true;option.textContent='No matching projects';sel.append(option)}
  if(preferredName&&[...sel.options].some(option=>option.value===preferredName))sel.value=preferredName;
}
function projectOption(p){const option=document.createElement('option');option.value=p.name;option.textContent=`${p.name} · ${p.width}×${p.height}`;return option}
async function listProjects(selectName){
  projectList=await api('/api/projects');
  if(!projectList.length){project=null;renderProjectOptions();renderEmpty();$('#newDialog').showModal();return}
  const target=selectName&&projectList.some(p=>p.name===selectName)?selectName:projectList[0].name;
  renderProjectOptions(target);await loadProject(target)
}
async function loadProject(name){project=await api(`/api/projects/${encodeURIComponent(name)}`);layerIndex=project.layers.length-1;frameIndex=0;colorIndex=0;undoStack=[];redoStack=[];dirty=false;renderProjectOptions(project.name);renderAll();toast(`Opened ${project.name}`)}
async function save(){if(!project)return;if(saving){dirty=true;return}saving=true;const payload=cloneProject();dirty=false;try{const saved=await api(`/api/projects/${encodeURIComponent(payload.name)}`,{method:'PUT',body:JSON.stringify(payload)});if(dirty){project.revision=saved.revision;project.updatedAt=saved.updatedAt}else project=saved;const listed=projectList.find(p=>p.name===project.name);if(listed){listed.category=project.category;listed.revision=project.revision;listed.updatedAt=project.updatedAt}renderProjectOptions(project.name);$('#saveProject').textContent='Saved ✓';setTimeout(()=>$('#saveProject').innerHTML='Save <kbd>Ctrl S</kbd>',900)}catch(e){dirty=true;toast(e.message)}finally{saving=false;if(dirty){clearTimeout(saveTimer);saveTimer=setTimeout(save,120)}}}
function changed(){dirty=true;renderAll();clearTimeout(saveTimer);saveTimer=setTimeout(save,500)}

function renderEmpty(){$('#projectTitle').textContent='Create a project to begin';$('#dimensions').textContent='';ctx.clearRect(0,0,canvas.width,canvas.height)}
function renderAll(){if(!project)return;renderCanvas();renderPalette();renderLayers();renderFrames();$('#projectTitle').textContent=project.name;$('#projectCategory').value=project.category;$('#dimensions').textContent=`${project.width} × ${project.height} · ${project.frameDurationsMs.length} frame${project.frameDurationsMs.length===1?'':'s'}`;$('#zoomLabel').textContent=`${zoom*100}%`;updateUndo()}
function hexRgb(h){return[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]}
function composite(frame){let out=new Uint8ClampedArray(project.width*project.height*4);project.layers.forEach(layer=>{if(!layer.visible)return;let pix=layer.frames[frame],a=Math.max(0,Math.min(1,layer.opacity));pix.forEach((pi,i)=>{if(pi<0||pi>=project.palette.length)return;let [r,g,b]=hexRgb(project.palette[pi]),da=out[i*4+3]/255,oa=a+da*(1-a);out[i*4]=(r*a+out[i*4]*da*(1-a))/oa;out[i*4+1]=(g*a+out[i*4+1]*da*(1-a))/oa;out[i*4+2]=(b*a+out[i*4+2]*da*(1-a))/oa;out[i*4+3]=oa*255})});return out}
function renderCanvas(){
  canvas.width=project.width*zoom;canvas.height=project.height*zoom;ctx.clearRect(0,0,canvas.width,canvas.height);
  if($('#onion').checked&&frameIndex>0){drawPixels(composite(frameIndex-1),.22)}drawPixels(composite(frameIndex),1);
  if(showGrid&&zoom>=8){ctx.strokeStyle='rgba(160,180,210,.16)';ctx.lineWidth=1;ctx.beginPath();for(let x=0;x<=project.width;x++){ctx.moveTo(x*zoom+.5,0);ctx.lineTo(x*zoom+.5,canvas.height)}for(let y=0;y<=project.height;y++){ctx.moveTo(0,y*zoom+.5);ctx.lineTo(canvas.width,y*zoom+.5)}ctx.stroke()}
}
function drawPixels(data,alpha){ctx.save();ctx.globalAlpha=alpha;for(let y=0;y<project.height;y++)for(let x=0;x<project.width;x++){let i=(y*project.width+x)*4;if(!data[i+3])continue;ctx.fillStyle=`rgba(${data[i]},${data[i+1]},${data[i+2]},${data[i+3]/255})`;ctx.fillRect(x*zoom,y*zoom,zoom,zoom)}ctx.restore()}
function renderPalette(){let p=$('#palette');p.innerHTML='';project.palette.forEach((c,i)=>{let b=document.createElement('button');b.className='swatch'+(i===colorIndex?' active':'');b.style.background=c;b.title=`${i}: ${c}`;b.onclick=()=>{colorIndex=i;renderPalette()};p.append(b)});$('#colorHex').textContent=project.palette[colorIndex]||'transparent'}
function renderLayers(){let box=$('#layers');box.innerHTML='';project.layers.forEach((l,i)=>{let d=document.createElement('div');d.className='layer'+(i===layerIndex?' active':'');d.innerHTML=`<button title="Toggle visibility">${l.visible?'◉':'○'}</button><div class="layer-name">${escapeHtml(l.name)}<br><small>${Math.round(l.opacity*100)}% · ${l.id.slice(0,5)}</small></div><small>#${i}</small>`;d.onclick=()=>{layerIndex=i;renderLayers()};d.children[0].onclick=e=>{e.stopPropagation();checkpoint();l.visible=!l.visible;changed()};d.ondblclick=()=>{let n=prompt('Layer name',l.name);if(n){checkpoint();l.name=n.trim();changed()}};box.append(d)})}
function renderFrames(){let box=$('#frames');box.innerHTML='';project.frameDurationsMs.forEach((duration,i)=>{let d=document.createElement('button');d.className='frame'+(i===frameIndex?' active':'');let c=document.createElement('canvas'),size=56;c.width=project.width;c.height=project.height;let cx=c.getContext('2d'),img=cx.createImageData(project.width,project.height);img.data.set(composite(i));cx.putImageData(img,0,0);c.style.width=`${Math.min(size,project.width*4)}px`;c.style.height=`${Math.min(size,project.height*4)}px`;let n=document.createElement('span');n.textContent=`${i+1} · ${duration}ms`;d.append(c,n);d.onclick=()=>{frameIndex=i;renderAll()};d.ondblclick=()=>{let value=prompt('Frame duration (ms)',duration);if(value&&+value>=16){checkpoint();project.frameDurationsMs[i]=Math.min(60000,+value);changed()}};box.append(d)})}
function escapeHtml(s){return s.replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]))}

function point(e){let r=canvas.getBoundingClientRect();return{x:Math.floor((e.clientX-r.left)*project.width/r.width),y:Math.floor((e.clientY-r.top)*project.height/r.height)}}
function currentPixels(){return project.layers[layerIndex].frames[frameIndex]}
function setPixel(x,y,c){if(x<0||y<0||x>=project.width||y>=project.height)return;currentPixels()[y*project.width+x]=c}
function line(a,b,c){let x0=a.x,y0=a.y,x1=b.x,y1=b.y,dx=Math.abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;while(true){setPixel(x0,y0,c);if(x0===x1&&y0===y1)break;let e2=2*err;if(e2>=dy){err+=dy;x0+=sx}if(e2<=dx){err+=dx;y0+=sy}}}
function rect(a,b,c){let x0=Math.min(a.x,b.x),x1=Math.max(a.x,b.x),y0=Math.min(a.y,b.y),y1=Math.max(a.y,b.y);line({x:x0,y:y0},{x:x1,y:y0},c);line({x:x0,y:y1},{x:x1,y:y1},c);line({x:x0,y:y0},{x:x0,y:y1},c);line({x:x1,y:y0},{x:x1,y:y1},c)}
function fill(x,y,c){if(x<0||y<0||x>=project.width||y>=project.height)return;let p=currentPixels(),target=p[y*project.width+x];if(target===c)return;let q=[[x,y]];while(q.length){let [px,py]=q.pop(),i=py*project.width+px;if(p[i]!==target)continue;p[i]=c;if(px)q.push([px-1,py]);if(px+1<project.width)q.push([px+1,py]);if(py)q.push([px,py-1]);if(py+1<project.height)q.push([px,py+1])}}
canvas.onpointerdown=e=>{if(!project)return;let p=point(e);canvas.setPointerCapture(e.pointerId);if(tool==='picker'){let pi=currentPixels()[p.y*project.width+p.x];if(pi>=0){colorIndex=pi;renderPalette()}return}checkpoint();drawing=true;startPoint=lastPoint=p;if(tool==='fill'){fill(p.x,p.y,colorIndex);drawing=false;changed();return}if(tool==='pencil'||tool==='eraser'){setPixel(p.x,p.y,tool==='eraser'?-1:colorIndex);renderCanvas()}};
canvas.onpointermove=e=>{if(!project)return;let p=point(e);$('#cursorInfo').textContent=`x ${p.x} · y ${p.y}`;if(!drawing)return;if(tool==='pencil'||tool==='eraser'){line(lastPoint,p,tool==='eraser'?-1:colorIndex);lastPoint=p;renderCanvas()}};
canvas.onpointerup=e=>{if(!drawing)return;let p=point(e);if(tool==='line')line(startPoint,p,colorIndex);if(tool==='rect')rect(startPoint,p,colorIndex);drawing=false;changed()};

$$('.tool[data-tool]').forEach(b=>b.onclick=()=>{$$('.tool[data-tool]').forEach(x=>x.classList.remove('active'));b.classList.add('active');tool=b.dataset.tool});
$('#undo').onclick=undo;$('#redo').onclick=redo;$('#gridToggle').onclick=()=>{showGrid=!showGrid;$('#gridToggle').classList.toggle('active',showGrid);renderCanvas()};
$('#zoomIn').onclick=()=>{zoom=Math.min(32,zoom+2);renderAll()};$('#zoomOut').onclick=()=>{zoom=Math.max(2,zoom-2);renderAll()};$('#onion').onchange=renderCanvas;
$('#addColor').onclick=()=>{if(project.palette.length>=256)return toast('Palette is full');let c=$('#colorInput').value;if(project.palette.includes(c)){colorIndex=project.palette.indexOf(c);renderPalette();return}checkpoint();project.palette.push(c);colorIndex=project.palette.length-1;changed()};
$('#addLayer').onclick=()=>{checkpoint();project.layers.push({id:crypto.randomUUID().replaceAll('-',''),name:`Layer ${project.layers.length+1}`,visible:true,opacity:1,frames:project.frameDurationsMs.map(()=>Array(project.width*project.height).fill(-1))});layerIndex=project.layers.length-1;changed()};
$('#deleteLayer').onclick=()=>{if(project.layers.length===1)return toast('Keep at least one layer');checkpoint();project.layers.splice(layerIndex,1);layerIndex=Math.min(layerIndex,project.layers.length-1);changed()};
$('#addFrame').onclick=()=>{checkpoint();project.frameDurationsMs.push(100);project.layers.forEach(l=>l.frames.push(Array(project.width*project.height).fill(-1)));frameIndex=project.frameDurationsMs.length-1;changed()};
$('#deleteFrame').onclick=()=>{if(project.frameDurationsMs.length===1)return toast('Keep at least one frame');checkpoint();project.frameDurationsMs.splice(frameIndex,1);project.layers.forEach(l=>l.frames.splice(frameIndex,1));frameIndex=Math.min(frameIndex,project.frameDurationsMs.length-1);changed()};
$('#play').onclick=()=>{playing=!playing;$('#play').textContent=playing?'■':'▶';clearTimeout(playTimer);const step=()=>{if(!playing)return;frameIndex=(frameIndex+1)%project.frameDurationsMs.length;renderAll();playTimer=setTimeout(step,project.frameDurationsMs[frameIndex])};if(playing)step()};
$('#saveProject').onclick=save;$('#projectSearch').oninput=()=>renderProjectOptions(project?.name);$('#projectSelect').onchange=e=>{if(dirty)save().then(()=>loadProject(e.target.value));else loadProject(e.target.value)};$('#projectCategory').onchange=e=>{if(!project||project.category===e.target.value)return;project.category=e.target.value;changed()};
$$('[data-export]').forEach(b=>b.onclick=()=>{if(!project)return;let f=b.dataset.export,scale=$('#exportScale').value,frame=frameIndex;window.location=`/api/projects/${encodeURIComponent(project.name)}/export/${f}?scale=${scale}&frame=${frame}`;toast(`Exported ${f}`)});

const dialog=$('#newDialog');$('#newProject').onclick=()=>dialog.showModal();$('#cancelNew').onclick=()=>dialog.close();
$('#newForm').onsubmit=async e=>{e.preventDefault();let f=new FormData(e.target);try{let p=await api('/api/projects',{method:'POST',body:JSON.stringify({name:f.get('name'),category:f.get('category'),width:+f.get('width'),height:+f.get('height'),frames:+f.get('frames')})});dialog.close();await listProjects(p.name)}catch(err){toast(err.message)}};
document.onkeydown=e=>{if(e.target.matches('input,select'))return;let key=e.key.toLowerCase();if((e.ctrlKey||e.metaKey)&&key==='s'){e.preventDefault();save()}else if((e.ctrlKey||e.metaKey)&&key==='z'){e.preventDefault();e.shiftKey?redo():undo()}else if((e.ctrlKey||e.metaKey)&&key==='y'){e.preventDefault();redo()}else{let map={p:'pencil',e:'eraser',f:'fill',i:'picker',l:'line',r:'rect'};if(map[key])document.querySelector(`[data-tool="${map[key]}"]`).click()}};
setInterval(async()=>{if(!project||dirty||saving)return;try{let fresh=await api(`/api/projects/${encodeURIComponent(project.name)}`);if(fresh.revision>project.revision){project=fresh;normalizeSelection();renderAll();toast('MCP changes synced')}}catch{}},1800);
listProjects().catch(e=>toast(e.message));
