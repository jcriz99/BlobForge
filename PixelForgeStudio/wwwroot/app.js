const $=s=>document.querySelector(s), $$=s=>[...document.querySelectorAll(s)];
const api=async(url,opts={})=>{const r=await fetch(url,{headers:{'Content-Type':'application/json',...(opts.headers||{})},...opts});if(!r.ok){let e=await r.json().catch(()=>({error:r.statusText}));throw Error(e.error||r.statusText)}return r.headers.get('content-type')?.includes('json')?r.json():r};
const projectCategories=['Tilesets','Objects','Misc Art'];
let project=null,projectList=[],layerIndex=0,frameIndex=0,colorIndex=0,tool='pencil',zoom=16,showGrid=true,dirty=false,saving=false,playing=false,playTimer=null;
let drawing=false,startPoint=null,lastPoint=null,undoStack=[],redoStack=[],saveTimer=null;
let selection=null,regionClipboard=null,referenceImage=null,productionReport=null,reportTimer=null;
const canvas=$('#canvas'),ctx=canvas.getContext('2d',{alpha:true});

function toast(message){let t=$('#toast');t.textContent=message;t.classList.add('show');clearTimeout(t._timer);t._timer=setTimeout(()=>t.classList.remove('show'),1800)}
function cloneProject(){return JSON.parse(JSON.stringify(project))}
function checkpoint(){if(!project)return;undoStack.push(cloneProject());if(undoStack.length>40)undoStack.shift();redoStack=[];updateUndo()}
function undo(){if(!undoStack.length)return;redoStack.push(cloneProject());project=undoStack.pop();normalizeSelection();changed()}
function redo(){if(!redoStack.length)return;undoStack.push(cloneProject());project=redoStack.pop();normalizeSelection();changed()}
function updateUndo(){$('#undo').disabled=!undoStack.length;$('#redo').disabled=!redoStack.length}
function normalizeSelection(){frameIndex=Math.min(frameIndex,project.frameDurationsMs.length-1);layerIndex=Math.min(layerIndex,project.layers.length-1);colorIndex=Math.min(colorIndex,project.palette.length-1)}
function normalizeProject(){project.paletteLocked=!!project.paletteLocked;project.reference??=null;project.attachmentPoints??=[];project.tags??=[];project.validation??={tileX:false,tileY:false,loop:false};project.validation.frameConsistency??=false;project.validation.attachmentMotion??=false;project.validation.maxOccupancyDriftPercent??=20;project.validation.maxBoundsDriftPixels??=2;project.validation.maxAttachmentStepPixels??=8;project.layers.forEach(layer=>layer.frameInvariant??=false);project.runtimeAssetName??=''}

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
async function loadProject(name){project=await api(`/api/projects/${encodeURIComponent(name)}`);normalizeProject();layerIndex=project.layers.length-1;frameIndex=0;colorIndex=0;selection=null;regionClipboard=null;undoStack=[];redoStack=[];dirty=false;productionReport=null;renderProjectOptions(project.name);await loadReferenceImage();renderAll();scheduleReport(0);toast(`Opened ${project.name}`)}
async function save(){if(!project)return;if(saving){dirty=true;return}saving=true;const payload=cloneProject();dirty=false;try{const saved=await api(`/api/projects/${encodeURIComponent(payload.name)}`,{method:'PUT',body:JSON.stringify(payload)});if(dirty){project.revision=saved.revision;project.updatedAt=saved.updatedAt}else{project=saved;normalizeProject()}const listed=projectList.find(p=>p.name===project.name);if(listed){listed.category=project.category;listed.revision=project.revision;listed.updatedAt=project.updatedAt}renderProjectOptions(project.name);scheduleReport(40);$('#saveProject').textContent='Saved ✓';setTimeout(()=>$('#saveProject').innerHTML='Save <kbd>Ctrl S</kbd>',900)}catch(e){dirty=true;toast(e.message)}finally{saving=false;if(dirty){clearTimeout(saveTimer);saveTimer=setTimeout(save,120)}}}
function changed(){dirty=true;renderAll();clearTimeout(saveTimer);saveTimer=setTimeout(save,500)}

function renderEmpty(){$('#projectTitle').textContent='Create a project to begin';$('#dimensions').textContent='';ctx.clearRect(0,0,canvas.width,canvas.height)}
function renderAll(){if(!project)return;renderCanvas();renderPalette();renderLayers();renderFrames();renderAnimationControls();renderProduction();$('#projectTitle').textContent=project.name;$('#projectCategory').value=project.category;$('#dimensions').textContent=`${project.width} × ${project.height} · ${project.frameDurationsMs.length} frame${project.frameDurationsMs.length===1?'':'s'}`;$('#zoomLabel').textContent=`${zoom*100}%`;updateUndo()}
function hexRgb(h){return[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]}
function composite(frame){let out=new Uint8ClampedArray(project.width*project.height*4);project.layers.forEach(layer=>{if(!layer.visible)return;let pix=layer.frames[frame],a=Math.max(0,Math.min(1,layer.opacity));pix.forEach((pi,i)=>{if(pi<0||pi>=project.palette.length)return;let [r,g,b]=hexRgb(project.palette[pi]),da=out[i*4+3]/255,oa=a+da*(1-a);out[i*4]=(r*a+out[i*4]*da*(1-a))/oa;out[i*4+1]=(g*a+out[i*4+1]*da*(1-a))/oa;out[i*4+2]=(b*a+out[i*4+2]*da*(1-a))/oa;out[i*4+3]=oa*255})});return out}
async function loadReferenceImage(){
  referenceImage=null;if(!project?.reference?.projectName)return;
  const image=new Image();image.style.imageRendering='pixelated';
  await new Promise(resolve=>{image.onload=()=>resolve();image.onerror=()=>resolve();image.src=`/api/projects/${encodeURIComponent(project.reference.projectName)}/preview/${project.reference.frame||0}?scale=1&revision=${project.revision}`});
  if(image.complete&&image.naturalWidth)referenceImage=image;
}
function scheduleReport(delay=120){clearTimeout(reportTimer);reportTimer=setTimeout(loadReport,delay)}
async function loadReport(){if(!project)return;try{productionReport=await api(`/api/projects/${encodeURIComponent(project.name)}/report`);renderProduction()}catch(e){$('#productionReport').textContent=e.message}}
function renderProduction(){
  if(!project)return;
  $('#paletteLocked').checked=project.paletteLocked;$('#addColor').disabled=project.paletteLocked;
  const refs=$('#referenceSelect'),selected=project.reference?.projectName||'';refs.innerHTML='<option value="">None</option>';
  projectList.filter(p=>p.name!==project.name).sort((a,b)=>a.name.localeCompare(b.name)).forEach(p=>{let o=document.createElement('option');o.value=p.name;o.textContent=p.name;refs.append(o)});refs.value=selected;
  const opacity=project.reference?.opacity??.35;$('#referenceOpacity').value=opacity;$('#referenceOpacity').disabled=!project.reference;$('#referenceOpacityLabel').textContent=`${Math.round(opacity*100)}%`;
  $('#validateTileX').checked=project.validation.tileX;$('#validateTileY').checked=project.validation.tileY;$('#validateLoop').checked=project.validation.loop;$('#validateFrames').checked=project.validation.frameConsistency;$('#validateAttachments').checked=project.validation.attachmentMotion;
  $('#runtimeAssetName').value=project.runtimeAssetName;$('#showComparison').disabled=!project.reference;
  $('#selectionStatus').textContent=selection?`Mask x${selection.x} y${selection.y} · ${selection.width}×${selection.height}`:'No selection mask';
  const attachments=$('#attachmentList');attachments.innerHTML='';project.attachmentPoints.forEach(point=>{let row=document.createElement('div');row.className='attachment-item';row.innerHTML=`<span>${escapeHtml(point.name)}</span><span>${point.x},${point.y}${point.frame==null?'':` · f${point.frame+1}`}</span><button title="Delete attachment">×</button>`;row.children[2].onclick=()=>{checkpoint();project.attachmentPoints=project.attachmentPoints.filter(p=>p!==point);changed()};attachments.append(row)});
  const box=$('#productionReport');if(!productionReport){box.textContent='Report loading…';return}
  const frame=productionReport.frames[frameIndex]||productionReport.frames[0],issues=productionReport.issues||[];
  box.innerHTML=`<strong>${productionReport.width}×${productionReport.height} · ${productionReport.frameCount} frame${productionReport.frameCount===1?'':'s'}</strong><br>`+
    `occupied ${frame.occupiedPixels} · transparent ${frame.transparentPixels}<br>`+
    `bounds ${frame.bounds?`${frame.bounds.x},${frame.bounds.y} ${frame.bounds.width}×${frame.bounds.height}`:'empty'} · components ${frame.silhouetteComponents}<br>`+
    `palette ${productionReport.palette.filter(p=>p.uses>0).length}/${productionReport.palette.length} · seams X${productionReport.tileXMismatches} Y${productionReport.tileYMismatches}<br>`+
    `loop endpoint Δ ${productionReport.loopEndpointDeltaPixels}px · clips ${productionReport.animation?.clips?.length||0}<br>`+
    `motion Δ ${productionReport.animation?.transitions?.[frameIndex]?.changedPixels??0}px · duplicate holds ${productionReport.animation?.adjacentDuplicateFrames??0}<br>`+
    (issues.length?issues.map(issue=>`<span class="${issue.severity}">${escapeHtml(issue.severity)} · ${escapeHtml(issue.message)}</span>`).join('<br>'):'<span class="ok">No configured validation issues</span>');
}
function renderCanvas(){
  canvas.width=project.width*zoom;canvas.height=project.height*zoom;ctx.clearRect(0,0,canvas.width,canvas.height);
  if(referenceImage){ctx.save();ctx.globalAlpha=project.reference?.opacity??.35;ctx.imageSmoothingEnabled=false;ctx.drawImage(referenceImage,0,0,referenceImage.naturalWidth*zoom,referenceImage.naturalHeight*zoom);ctx.restore()}
  if($('#onion').checked){const previous=Math.max(0,Math.min(4,+$('#onionPrev').value||0)),next=Math.max(0,Math.min(4,+$('#onionNext').value||0));for(let offset=previous;offset>=1;offset--)if(frameIndex-offset>=0)drawPixels(composite(frameIndex-offset),.12+(previous-offset)*.04,'#ff5d7a');for(let offset=next;offset>=1;offset--)if(frameIndex+offset<project.frameDurationsMs.length)drawPixels(composite(frameIndex+offset),.12+(next-offset)*.04,'#56b5ff')}drawPixels(composite(frameIndex),1);
  if(showGrid&&zoom>=8){ctx.strokeStyle='rgba(160,180,210,.16)';ctx.lineWidth=1;ctx.beginPath();for(let x=0;x<=project.width;x++){ctx.moveTo(x*zoom+.5,0);ctx.lineTo(x*zoom+.5,canvas.height)}for(let y=0;y<=project.height;y++){ctx.moveTo(0,y*zoom+.5);ctx.lineTo(canvas.width,y*zoom+.5)}ctx.stroke()}
  if(selection){ctx.save();ctx.strokeStyle='#63d4df';ctx.lineWidth=2;ctx.setLineDash([Math.max(2,zoom/2),Math.max(2,zoom/3)]);ctx.strokeRect(selection.x*zoom+1,selection.y*zoom+1,selection.width*zoom-2,selection.height*zoom-2);ctx.restore()}
  project.attachmentPoints.filter(point=>point.frame==null||point.frame===frameIndex).forEach(point=>{const x=(point.x+.5)*zoom,y=(point.y+.5)*zoom,r=Math.max(4,zoom*.55);ctx.save();ctx.strokeStyle='#ffc566';ctx.fillStyle='#090b11dd';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(x-r,y);ctx.lineTo(x+r,y);ctx.moveTo(x,y-r);ctx.lineTo(x,y+r);ctx.stroke();ctx.font=`${Math.max(9,zoom*.65)}px ui-monospace`;ctx.fillText(point.name,x+r+2,y-2);ctx.restore()})
}
function setZoom(nextZoom,anchor){
  if(!project)return;
  const clamped=Math.max(2,Math.min(32,nextZoom));
  if(clamped===zoom)return;
  const wrap=$('#canvasWrap'),before=canvas.getBoundingClientRect();
  const anchorX=anchor?.clientX??(before.left+before.width/2),anchorY=anchor?.clientY??(before.top+before.height/2);
  const relativeX=before.width?(anchorX-before.left)/before.width:.5,relativeY=before.height?(anchorY-before.top)/before.height:.5;
  zoom=clamped;renderCanvas();$('#zoomLabel').textContent=`${zoom*100}%`;
  const after=canvas.getBoundingClientRect();
  wrap.scrollLeft+=after.left+relativeX*after.width-anchorX;
  wrap.scrollTop+=after.top+relativeY*after.height-anchorY;
}
function drawPixels(data,alpha,tint){ctx.save();ctx.globalAlpha=alpha;const tinted=tint?hexRgb(tint):null;for(let y=0;y<project.height;y++)for(let x=0;x<project.width;x++){let i=(y*project.width+x)*4;if(!data[i+3])continue;const rgb=tinted||[data[i],data[i+1],data[i+2]];ctx.fillStyle=`rgba(${rgb[0]},${rgb[1]},${rgb[2]},${data[i+3]/255})`;ctx.fillRect(x*zoom,y*zoom,zoom,zoom)}ctx.restore()}
function renderPalette(){let p=$('#palette');p.innerHTML='';project.palette.forEach((c,i)=>{let b=document.createElement('button');b.className='swatch'+(i===colorIndex?' active':'');b.style.background=c;b.title=`${i}: ${c}`;b.onclick=()=>{colorIndex=i;renderPalette()};p.append(b)});$('#colorHex').textContent=project.palette[colorIndex]||'transparent'}
function renderLayers(){let box=$('#layers');box.innerHTML='';project.layers.forEach((l,i)=>{let d=document.createElement('div');d.className='layer'+(i===layerIndex?' active':'');d.innerHTML=`<button title="Toggle visibility">${l.visible?'◉':'○'}</button><div class="layer-name">${escapeHtml(l.name)}<br><small>${Math.round(l.opacity*100)}% · ${l.id.slice(0,5)}</small></div><small>#${i}</small>`;d.onclick=()=>{layerIndex=i;renderLayers()};d.children[0].onclick=e=>{e.stopPropagation();checkpoint();l.visible=!l.visible;changed()};d.ondblclick=()=>{let n=prompt('Layer name',l.name);if(n){checkpoint();l.name=n.trim();changed()}};box.append(d)})}
function renderFrames(){let box=$('#frames');box.innerHTML='';project.frameDurationsMs.forEach((duration,i)=>{let d=document.createElement('button');d.className='frame'+(i===frameIndex?' active':'');let c=document.createElement('canvas'),size=56;c.width=project.width;c.height=project.height;let cx=c.getContext('2d'),img=cx.createImageData(project.width,project.height);img.data.set(composite(i));cx.putImageData(img,0,0);c.style.width=`${Math.min(size,project.width*4)}px`;c.style.height=`${Math.min(size,project.height*4)}px`;let n=document.createElement('span');n.textContent=`${i+1} · ${duration}ms`;d.append(c,n);d.onclick=()=>{frameIndex=i;renderAll()};d.ondblclick=()=>{let value=prompt('Frame duration (ms)',duration);if(value&&+value>=16){checkpoint();project.frameDurationsMs[i]=Math.min(60000,+value);changed()}};box.append(d)})}
function renderAnimationControls(){const select=$('#animationClip'),selected=select.value;select.innerHTML='<option value="">All frames</option>';project.tags.forEach(tag=>{const option=document.createElement('option');option.value=tag.name;option.textContent=`${tag.name} · ${tag.direction}${tag.loop?' ↻':''}`;select.append(option)});select.value=project.tags.some(tag=>tag.name===selected)?selected:'';$('#deleteClip').disabled=!select.value}
function selectedAnimation(){return project.tags.find(tag=>tag.name===$('#animationClip').value)||null}
function playbackFrames(tag){let frames=Array.from({length:(tag?.to??project.frameDurationsMs.length-1)-(tag?.from??0)+1},(_,i)=>(tag?.from??0)+i);if(tag?.direction==='reverse')frames.reverse();else if(tag?.direction==='pingpong'&&frames.length>1)frames=frames.concat(frames.slice(1,-1).reverse());return frames}
function escapeHtml(s){return s.replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]))}

function point(e){let r=canvas.getBoundingClientRect();return{x:Math.floor((e.clientX-r.left)*project.width/r.width),y:Math.floor((e.clientY-r.top)*project.height/r.height)}}
function currentPixels(){return project.layers[layerIndex].frames[frameIndex]}
function setPixel(x,y,c){if(x<0||y<0||x>=project.width||y>=project.height)return;currentPixels()[y*project.width+x]=c}
function line(a,b,c){let x0=a.x,y0=a.y,x1=b.x,y1=b.y,dx=Math.abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;while(true){setPixel(x0,y0,c);if(x0===x1&&y0===y1)break;let e2=2*err;if(e2>=dy){err+=dy;x0+=sx}if(e2<=dx){err+=dx;y0+=sy}}}
function rect(a,b,c){let x0=Math.min(a.x,b.x),x1=Math.max(a.x,b.x),y0=Math.min(a.y,b.y),y1=Math.max(a.y,b.y);line({x:x0,y:y0},{x:x1,y:y0},c);line({x:x0,y:y1},{x:x1,y:y1},c);line({x:x0,y:y0},{x:x0,y:y1},c);line({x:x1,y:y0},{x:x1,y:y1},c)}
function fill(x,y,c){if(x<0||y<0||x>=project.width||y>=project.height)return;let p=currentPixels(),target=p[y*project.width+x];if(target===c)return;let q=[[x,y]];while(q.length){let [px,py]=q.pop(),i=py*project.width+px;if(p[i]!==target)continue;p[i]=c;if(px)q.push([px-1,py]);if(px+1<project.width)q.push([px+1,py]);if(py)q.push([px,py-1]);if(py+1<project.height)q.push([px,py+1])}}
function selectionFrom(a,b){let x=Math.min(a.x,b.x),y=Math.min(a.y,b.y);return{x,y,width:Math.max(1,Math.abs(a.x-b.x)+1),height:Math.max(1,Math.abs(a.y-b.y)+1)}}
function copySelection(){if(!selection)return toast('Select a rectangular mask first');let pixels=[];for(let y=0;y<selection.height;y++)for(let x=0;x<selection.width;x++)pixels.push(currentPixels()[(selection.y+y)*project.width+selection.x+x]);regionClipboard={width:selection.width,height:selection.height,pixels};toast(`Copied ${selection.width}×${selection.height}`)}
function pasteSelection(){if(!regionClipboard)return toast('Nothing copied');let x=selection?.x??0,y=selection?.y??0;checkpoint();for(let py=0;py<regionClipboard.height;py++)for(let px=0;px<regionClipboard.width;px++)setPixel(x+px,y+py,regionClipboard.pixels[py*regionClipboard.width+px]);selection={x,y,width:Math.min(regionClipboard.width,project.width-x),height:Math.min(regionClipboard.height,project.height-y)};changed()}
function transformSelection(operation){if(!selection)return toast('Select a rectangular mask first');checkpoint();let source=[];for(let y=0;y<selection.height;y++)for(let x=0;x<selection.width;x++)source.push(currentPixels()[(selection.y+y)*project.width+selection.x+x]);for(let y=0;y<selection.height;y++)for(let x=0;x<selection.width;x++){let sx=x,sy=y;if(operation==='flip-horizontal')sx=selection.width-1-x;if(operation==='flip-vertical')sy=selection.height-1-y;if(operation==='rotate-180'){sx=selection.width-1-x;sy=selection.height-1-y}setPixel(selection.x+x,selection.y+y,source[sy*selection.width+sx])}changed()}
canvas.onpointerdown=e=>{if(!project)return;let p=point(e);canvas.setPointerCapture(e.pointerId);if(tool==='picker'){let pi=currentPixels()[p.y*project.width+p.x];if(pi>=0){colorIndex=pi;renderPalette()}return}drawing=true;startPoint=lastPoint=p;if(tool==='select'){selection=selectionFrom(p,p);renderCanvas();renderProduction();return}checkpoint();if(tool==='fill'){fill(p.x,p.y,colorIndex);drawing=false;changed();return}if(tool==='pencil'||tool==='eraser'){setPixel(p.x,p.y,tool==='eraser'?-1:colorIndex);renderCanvas()}};
canvas.onpointermove=e=>{if(!project)return;let p=point(e);$('#cursorInfo').textContent=`x ${p.x} · y ${p.y}`;if(!drawing)return;if(tool==='select'){selection=selectionFrom(startPoint,p);renderCanvas();renderProduction();return}if(tool==='pencil'||tool==='eraser'){line(lastPoint,p,tool==='eraser'?-1:colorIndex);lastPoint=p;renderCanvas()}};
canvas.onpointerup=e=>{if(!drawing)return;let p=point(e);if(tool==='select'){selection=selectionFrom(startPoint,p);drawing=false;renderCanvas();renderProduction();return}if(tool==='line')line(startPoint,p,colorIndex);if(tool==='rect')rect(startPoint,p,colorIndex);drawing=false;changed()};

$$('.tool[data-tool]').forEach(b=>b.onclick=()=>{$$('.tool[data-tool]').forEach(x=>x.classList.remove('active'));b.classList.add('active');tool=b.dataset.tool});
$('#undo').onclick=undo;$('#redo').onclick=redo;$('#gridToggle').onclick=()=>{showGrid=!showGrid;$('#gridToggle').classList.toggle('active',showGrid);renderCanvas()};
$('#zoomIn').onclick=()=>setZoom(zoom+2);$('#zoomOut').onclick=()=>setZoom(zoom-2);$('#onion').onchange=renderCanvas;$('#onionPrev').oninput=renderCanvas;$('#onionNext').oninput=renderCanvas;
$('#canvasWrap').addEventListener('wheel',e=>{
  if(!project)return;
  const wrap=$('#canvasWrap');
  if(e.shiftKey){
    e.preventDefault();
    if(wrap.scrollHeight>wrap.clientHeight)wrap.scrollTop+=e.deltaY||e.deltaX;
    return;
  }
  e.preventDefault();setZoom(zoom+(e.deltaY<0?2:-2),e)
},{passive:false});
$('#addColor').onclick=()=>{if(project.paletteLocked)return toast('Unlock the approved palette before adding colors');if(project.palette.length>=256)return toast('Palette is full');let c=$('#colorInput').value;if(project.palette.includes(c)){colorIndex=project.palette.indexOf(c);renderPalette();return}checkpoint();project.palette.push(c);colorIndex=project.palette.length-1;changed()};
$('#paletteLocked').onchange=e=>{checkpoint();project.paletteLocked=e.target.checked;changed();scheduleReport()};
$('#referenceSelect').onchange=async e=>{checkpoint();project.reference=e.target.value?{projectName:e.target.value,frame:0,opacity:+$('#referenceOpacity').value}:null;await loadReferenceImage();changed();scheduleReport()};
$('#referenceOpacity').oninput=e=>{if(!project.reference)return;project.reference.opacity=+e.target.value;$('#referenceOpacityLabel').textContent=`${Math.round(project.reference.opacity*100)}%`;dirty=true;clearTimeout(saveTimer);saveTimer=setTimeout(save,500);renderCanvas()};
$('#validateTileX').onchange=e=>{checkpoint();project.validation.tileX=e.target.checked;changed();scheduleReport()};
$('#validateTileY').onchange=e=>{checkpoint();project.validation.tileY=e.target.checked;changed();scheduleReport()};
$('#validateLoop').onchange=e=>{checkpoint();project.validation.loop=e.target.checked;changed();scheduleReport()};
$('#validateFrames').onchange=e=>{checkpoint();project.validation.frameConsistency=e.target.checked;changed();scheduleReport()};
$('#validateAttachments').onchange=e=>{checkpoint();project.validation.attachmentMotion=e.target.checked;changed();scheduleReport()};
$('#refreshReport').onclick=()=>scheduleReport(0);
$('#copySelection').onclick=copySelection;$('#pasteSelection').onclick=pasteSelection;
$('#flipSelectionH').onclick=()=>transformSelection('flip-horizontal');$('#flipSelectionV').onclick=()=>transformSelection('flip-vertical');$('#rotateSelection').onclick=()=>transformSelection('rotate-180');
$('#addAttachment').onclick=()=>{let name=$('#attachmentName').value.trim();if(!name)return toast('Name the attachment point');let x=$('#attachmentX').value===''&&selection?selection.x+Math.floor(selection.width/2):+$('#attachmentX').value;let y=$('#attachmentY').value===''&&selection?selection.y+Math.floor(selection.height/2):+$('#attachmentY').value;if(!Number.isFinite(x)||!Number.isFinite(y)||x<0||y<0||x>=project.width||y>=project.height)return toast('Attachment point must be inside the canvas');const frame=$('#attachmentCurrentFrame').checked?frameIndex:null;checkpoint();project.attachmentPoints=project.attachmentPoints.filter(p=>p.name.toLowerCase()!==name.toLowerCase()||p.frame!==frame);project.attachmentPoints.push({name,x:Math.round(x),y:Math.round(y),frame});$('#attachmentName').value='';changed();scheduleReport()};
$('#runtimeAssetName').onchange=e=>{checkpoint();project.runtimeAssetName=e.target.value.trim();changed()};
const previewDialog=$('#previewDialog');$('#closePreviewDialog').onclick=()=>previewDialog.close();
function showProductionPreview(title,note,url){$('#previewDialogTitle').textContent=title;$('#previewDialogNote').textContent=note;$('#previewDialogImage').src=url+(url.includes('?')?'&':'?')+`t=${Date.now()}`;previewDialog.showModal()}
$('#showContactSheet').onclick=async()=>{await save();const tag=selectedAnimation();const url=tag?`/api/projects/${encodeURIComponent(project.name)}/animation-contact-sheet?tag=${encodeURIComponent(tag.name)}&scale=4`:`/api/projects/${encodeURIComponent(project.name)}/contact-sheet?scale=4`;showProductionPreview(`${project.name} · ${tag?.name||'contact sheet'}`,tag?`${tag.direction}${tag.loop?' loop':' once'} · frames ${tag.from+1}–${tag.to+1}`:`${project.frameDurationsMs.length} frames · ${project.frameDurationsMs.reduce((a,b)=>a+b,0)}ms total`,url)};
$('#showComparison').onclick=async()=>{if(!project.reference)return toast('Choose an approved reference first');await save();showProductionPreview(`${project.name} · comparison`,`Current asset on the left · ${project.reference.projectName} on the right`,`/api/projects/${encodeURIComponent(project.name)}/comparison?frame=${frameIndex}&scale=4`)};
async function sendToBlobForge(launch){await save();try{let result=await api(`/api/projects/${encodeURIComponent(project.name)}/preview-in-blobforge`,{method:'POST',body:JSON.stringify({launch})});toast(result.warning||`${result.assetName} ${launch?'launched':'sent to BlobForge'}`)}catch(e){toast(e.message)}}
$('#sendToBlobForge').onclick=()=>sendToBlobForge(false);$('#previewInBlobForge').onclick=()=>sendToBlobForge(true);
$('#addLayer').onclick=()=>{checkpoint();project.layers.push({id:crypto.randomUUID().replaceAll('-',''),name:`Layer ${project.layers.length+1}`,visible:true,opacity:1,frameInvariant:false,frames:project.frameDurationsMs.map(()=>Array(project.width*project.height).fill(-1))});layerIndex=project.layers.length-1;changed()};
$('#deleteLayer').onclick=()=>{if(project.layers.length===1)return toast('Keep at least one layer');checkpoint();project.layers.splice(layerIndex,1);layerIndex=Math.min(layerIndex,project.layers.length-1);changed()};
$('#addFrame').onclick=()=>{checkpoint();project.frameDurationsMs.push(100);project.layers.forEach(l=>l.frames.push(Array(project.width*project.height).fill(-1)));frameIndex=project.frameDurationsMs.length-1;changed()};
$('#deleteFrame').onclick=()=>{if(project.frameDurationsMs.length===1)return toast('Keep at least one frame');checkpoint();const deleted=frameIndex;project.frameDurationsMs.splice(deleted,1);project.layers.forEach(l=>l.frames.splice(deleted,1));project.attachmentPoints=project.attachmentPoints.filter(point=>point.frame!==deleted).map(point=>({...point,frame:point.frame!=null&&point.frame>deleted?point.frame-1:point.frame}));project.tags=project.tags.map(tag=>{if(deleted<tag.from)return{...tag,from:tag.from-1,to:tag.to-1};if(deleted<=tag.to)return{...tag,to:tag.to-1};return tag}).filter(tag=>tag.to>=tag.from);frameIndex=Math.min(deleted,project.frameDurationsMs.length-1);changed()};
function stopPlayback(){playing=false;clearTimeout(playTimer);$('#play').textContent='▶'}
$('#play').onclick=()=>{if(playing)return stopPlayback();const tag=selectedAnimation(),sequence=playbackFrames(tag);let cursor=Math.max(0,sequence.indexOf(frameIndex));playing=true;$('#play').textContent='■';const step=()=>{if(!playing)return;frameIndex=sequence[cursor];renderAll();playTimer=setTimeout(()=>{cursor++;if(cursor>=sequence.length){if(tag?.loop??true)cursor=0;else return stopPlayback()}step()},project.frameDurationsMs[frameIndex])};step()};
$('#animationClip').onchange=()=>{stopPlayback();const tag=selectedAnimation();if(tag)frameIndex=tag.direction==='reverse'?tag.to:tag.from;renderAll()};
$('#addClip').onclick=()=>{const name=prompt('Animation clip name','idle')?.trim();if(!name)return;const from=Math.max(0,(+(prompt('First frame (1-based)','1')||1))-1),to=Math.min(project.frameDurationsMs.length-1,(+(prompt('Last frame (1-based)',String(project.frameDurationsMs.length))||project.frameDurationsMs.length))-1);if(to<from)return toast('Clip frame range is invalid');const direction=(prompt('Direction: forward, reverse, or pingpong','forward')||'forward').toLowerCase();if(!['forward','reverse','pingpong'].includes(direction))return toast('Unknown clip direction');const loop=confirm('Should this animation loop?');checkpoint();project.tags=project.tags.filter(tag=>tag.name.toLowerCase()!==name.toLowerCase());project.tags.push({name,from,to,direction,loop});changed();setTimeout(()=>{$('#animationClip').value=name;renderAnimationControls()},0)};
$('#deleteClip').onclick=()=>{const name=$('#animationClip').value;if(!name)return;checkpoint();project.tags=project.tags.filter(tag=>tag.name!==name);changed()};
$('#saveProject').onclick=save;$('#projectSearch').oninput=()=>renderProjectOptions(project?.name);$('#projectSelect').onchange=e=>{if(dirty)save().then(()=>loadProject(e.target.value));else loadProject(e.target.value)};$('#projectCategory').onchange=e=>{if(!project||project.category===e.target.value)return;project.category=e.target.value;changed()};
$$('[data-export]').forEach(b=>b.onclick=()=>{if(!project)return;let f=b.dataset.export,scale=$('#exportScale').value,frame=frameIndex;window.location=`/api/projects/${encodeURIComponent(project.name)}/export/${f}?scale=${scale}&frame=${frame}`;toast(`Exported ${f}`)});

const dialog=$('#newDialog');$('#newProject').onclick=()=>dialog.showModal();$('#cancelNew').onclick=()=>dialog.close();
$('#newForm').onsubmit=async e=>{e.preventDefault();let f=new FormData(e.target);try{let p=await api('/api/projects',{method:'POST',body:JSON.stringify({name:f.get('name'),category:f.get('category'),width:+f.get('width'),height:+f.get('height'),frames:+f.get('frames')})});dialog.close();await listProjects(p.name)}catch(err){toast(err.message)}};
document.onkeydown=e=>{if(e.target.matches('input,select'))return;let key=e.key.toLowerCase();if((e.ctrlKey||e.metaKey)&&key==='s'){e.preventDefault();save()}else if((e.ctrlKey||e.metaKey)&&key==='c'){e.preventDefault();copySelection()}else if((e.ctrlKey||e.metaKey)&&key==='v'){e.preventDefault();pasteSelection()}else if((e.ctrlKey||e.metaKey)&&key==='z'){e.preventDefault();e.shiftKey?redo():undo()}else if((e.ctrlKey||e.metaKey)&&key==='y'){e.preventDefault();redo()}else{let map={p:'pencil',e:'eraser',f:'fill',i:'picker',l:'line',r:'rect',s:'select'};if(map[key])document.querySelector(`[data-tool="${map[key]}"]`).click()}};
setInterval(async()=>{if(!project||dirty||saving)return;try{let fresh=await api(`/api/projects/${encodeURIComponent(project.name)}`);if(fresh.revision>project.revision){project=fresh;normalizeProject();normalizeSelection();await loadReferenceImage();renderAll();scheduleReport(0);toast('MCP changes synced')}}catch{}},1800);
listProjects().catch(e=>toast(e.message));
