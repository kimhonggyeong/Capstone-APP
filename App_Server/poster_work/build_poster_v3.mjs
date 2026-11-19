import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {FileBlob, PresentationFile} from 'file:///C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
import {finalizePresentation} from 'file:///C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations/container_tools/artifact_tool_utils.mjs';
process.on('uncaughtException',error=>{console.error(error.message);process.exitCode=1;});

const workspaceDir='C:/Users/hk010/OneDrive/바탕 화면/Full_Server';
const skillDir='C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations';
const source='C:/Users/hk010/OneDrive/Documents/카카오톡 받은 파일/[Template 12] 전시회 판넬 [C-3조] ANTITUDE_시나리오 기반 AI 투자 판단 학습 플랫폼.pptx';
const work=path.join(workspaceDir,'poster_work');
const finalPath=path.join(workspaceDir,'outputs','ANTITUDE_전시회판넬_웹앱공통기능_아키텍처수정.pptx');
const p=await PresentationFile.importPptx(await FileBlob.load(source));
const slide=p.slides.items[0];
const report=await p.inspect({select:'$.slides[0].elements[*]',include:['id','name','kind','text','position','style']});
const records=report.records;
const rec=n=>records.find(x=>x.name===`Google Shape;${n};p13`);
const find=n=>p.resolve(rec(n).id);
function replace(n,text,style={}){const s=find(n);s.text=text;s.text.style=style;return s;}
function place(n,position){find(n).position=position;}
function del(n){p.delete(rec(n).id);}
function txt(name,text,x,y,w,h,size=24,bold=false,color='#18334E'){
 const s=slide.shapes.add({name,geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});
 s.text=text;
 s.text.style={typeface:'Malgun Gothic',fontSize:size,bold,color,alignment:'center',verticalAlignment:'middle',insets:0,lineSpacing:1.05};
 return s;
}

// Functional descriptions are shared by both clients, as confirmed by the user.
const goals=replace(99,[
 '투자 초보자가 뉴스와 공시의 핵심 요인·위험을 해석하고 자신의 판단 근거를 점검하도록 지원',
 '웹 브라우저와 모바일 앱에서 시나리오 실습과 실제 시세 기반 모의투자를 반복 학습',
 '판단 근거와 실제 매매 행동을 함께 평가하고 AI 해설을 통해 정보 해석·위험 인식 연습'
].join('\n'),{fontSize:28,typeface:'Malgun Gothic',lineSpacing:1.1});
goals.position={...rec(99).position,height:155.13};
for(const n of [103,104,105])place(n,{...rec(n).position,top:rec(n).position.top-40});
replace(104,'웹·앱 공통 기능 및 특징');
const features=replace(100,[
 '실시간 모의투자: 종목 시세·차트·호가·뉴스를 확인하고 가상 주문·포트폴리오·수익률 관리',
 '시나리오 학습: 과거 금융 이벤트의 턴별 정보를 해석하고 매매 행동과 판단 근거 제출',
 '판단 과정 평가: 핵심 요인·위험 인식·행동 정합성을 평가하고 턴별 피드백·최종 코칭 제공',
 'AI 판단 비교: 매수·매도·관망의 근거와 신뢰도를 확인하고 내 판단 비교·히스토리 복습',
 '시장 반응 분석: 뉴스·공시·가정 상황을 입력하고 공시 RAG·투자자 관점으로 영향 분석',
 '기초 학습과 기록: 금융 용어·퀴즈, 시나리오 이어하기·재도전, 마이페이지의 자산·진행 관리'
].join('\n'),{fontSize:27,typeface:'Malgun Gothic',lineSpacing:1.05});
features.position={...rec(100).position,top:673.67,height:240};
replace(110,'시스템 아키텍처');
replace(101,[
 '클라이언트: React·TypeScript(웹), Unity·C#(앱) / 서버: Node.js·Express, Python·FastAPI',
 'AI·데이터: OpenAI·Gemini·Ollama, FAISS, KIS Open API, 뉴스·DART·SEC EDGAR',
 '운영: PC 웹 브라우저·Android 앱, MongoDB Atlas, 외부 AI API 및 로컬 LLM 연동'
].join('\n'),{fontSize:26,typeface:'Malgun Gothic',lineSpacing:1.1});
replace(102,[
 '가상 자금과 과거 금융 이벤트를 활용하여 금전 손실 없이 투자 판단과 주문 과정을 반복 학습',
 '판단 근거와 실제 행동을 AI 피드백·판단 비교·히스토리로 점검하고 학습 내용을 복습',
 '웹·앱의 공통 학습 기능을 활용한 금융 입문 교육, 대학 실습 및 자기주도 학습 도구로 활용'
].join('\n'),{fontSize:27,typeface:'Malgun Gothic',lineSpacing:1.1});

// Keep the original server, AI, database, API icons and their architecture links.
// Replace only the actor/use-case area with two concrete client applications.
for(const n of [138,139,142,143,155,156,157,158,159,165])del(n);
replace(144,'웹·앱 클라이언트',{fontSize:28,typeface:'Malgun Gothic',bold:true});
place(144,{left:140,top:1068,width:410,height:38});
const web=slide.shapes.add({name:'web-client',geometry:'rect',position:{left:140,top:1120,width:410,height:205},fill:'#FFFFFF',line:{fill:'#8C9CAF',width:1.5}});
place(140,{left:165,top:1177,width:92,height:92});
find(140).bringToFront();
replace(141,'웹 브라우저',{fontSize:28,typeface:'Malgun Gothic',bold:true});
place(141,{left:275,top:1170,width:255,height:43});
find(141).bringToFront();
txt('web-tech-label','React·TypeScript',275,1220,255,39,24);

const app=slide.shapes.add({name:'mobile-client',geometry:'rect',position:{left:140,top:1380,width:410,height:205},fill:'#FFFFFF',line:{fill:'#8C9CAF',width:1.5}});
const unityBytes=await fs.readFile(path.join(work,'unity-original.svg'));
slide.images.add({blob:unityBytes,contentType:'image/svg+xml',alt:'Unity 로고',fit:'contain',position:{left:165,top:1437,width:92,height:92}});
txt('mobile-title','모바일 앱',275,1430,255,43,28,true);
txt('mobile-tech-label','Unity·C#',275,1480,255,39,24);

for(const [x,y,w,h,arrow] of [[550,1222.5,60,0,false],[550,1482.5,60,0,false],[610,1206,0,276.5,false],[610,1206,65,0,true]]){
 const c=slide.shapes.add({geometry:'connector',kind:'straight',position:{left:x,top:y,width:w,height:h},line:{style:'solid',fill:'#53697E',width:2},...(arrow?{tail:{type:'triangle',width:'med',length:'med'}}:{})});
 c.bringToFront();
}
txt('client-server-protocol','HTTP(S)',553,1119,116,30,20);
txt('client-server-api','API 요청·응답',552,1152,122,29,18);

replace(123,'서비스 API 서버',{fontSize:28,typeface:'Malgun Gothic',bold:true});
replace(128,'인증·주문·자산 / 시나리오·AI 요청',{fontSize:22,typeface:'Malgun Gothic'});
replace(137,'공시 검색(RAG)·투자자 관점 분석\nAI 판단·규칙 기반 평가·AI 코칭',{fontSize:23,typeface:'Malgun Gothic',lineSpacing:1});
replace(148,'MongoDB Atlas',{fontSize:23,typeface:'Malgun Gothic'});
replace(149,'사용자·주문\n학습·판단 기록',{fontSize:20,typeface:'Malgun Gothic',lineSpacing:1});
place(149,{left:1258,top:1278,width:214,height:46});

slide.speakerNotes.append('\n\n웹·앱 공통 기능 및 시스템 아키텍처 수정 근거:\n'+[
 '사용자 확인: 웹과 앱은 기본적으로 같은 기능을 제공함. 기능 목록은 플랫폼별로 분리하지 않음.',
 '웹 소스: C:/Users/hk010/OneDrive/바탕 화면/Capstone-ver0.1-main/',
 'app/src/services/api.service.tsx: 웹 클라이언트는 /api로 HTTP 요청',
 'app/src/services/scenario.service.ts 및 server/src/routes.ts: 시나리오 요청을 Express에서 FastAPI 서비스로 전달',
 'app/src/services/judgment.service.ts: AI 판단·비교·히스토리',
 'server/src/controller/marketReaction.controller.ts: 시장 반응 FastAPI 연동',
 'app/src/pages/FinanceLearning.tsx: 금융 용어와 퀴즈',
 '앱 소스: C:/Users/hk010/Ant Teacher_V2/Assets/Scripts/',
 '앱 공통 서버: C:/Users/hk010/OneDrive/바탕 화면/Full_Server/main.py',
 '구성도는 원본과 같은 논리적 시스템 아키텍처이며 각 서비스의 물리적 배포 위치나 포트를 특정하지 않음.',
 '클라이언트의 기능 사용 순서나 사용 사례는 구성도에서 제외함.',
 '기술 로고: 원본 판넬의 React, Node.js, FastAPI, Python, Ollama, Gemini, MongoDB, 외부 API 이미지 보존.',
 '추가 Unity 로고 출처: https://raw.githubusercontent.com/devicons/devicon/master/icons/unity/unity-original.svg (Devicon 저장소)'
].join('\n'));

const draft=path.join(work,'candidate-v3.pptx');
await(await PresentationFile.exportPptx(p)).save(draft);
const image=await slide.export({format:'png',scale:1});
await fs.writeFile(path.join(work,'v3-preview.png'),new Uint8Array(await image.arrayBuffer()));
const layout=await slide.export({format:'layout',detail:'full'});
await fs.writeFile(path.join(work,'v3-layout.json'),await layout.text());
const out=await p.inspect({select:'$.slides[0].elements[*]',include:['id','name','kind','text']});
await fs.writeFile(path.join(work,'v3-inspect.ndjson'),out.ndjson);
const required=[124,125,131,132,133,140,147,152].map(n=>rec(n).name);
for(const name of required){if(!out.records.some(x=>x.name===name&&x.kind==='image'))throw new Error('Missing original image '+name);}
console.log('Original architecture logos retained:',required.length,'; added Unity logo.');

if(process.argv.includes('--finalize')){
 const result=await finalizePresentation({
  workspaceDir,candidatePath:draft,finalPath,
  pythonExecutable:'C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
  integrityValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_package_integrity.py'),
  layoutValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_layout_geometry.py'),
  layoutArgs:['--expected-slide-size-emu','15083150,21331550','--validate-bullet-geometry','--validate-heading-fit'],
  explicitTotalSlideCount:1,requiredNativeTableOwnerSlides:[],requiredNativeChartOwnerSlides:[],
  fontPolicy:{basis:'reference',families:['Malgun Gothic','Arial','Batang'],referencePath:source,referenceSha256:createHash('sha256').update(await fs.readFile(source)).digest('hex')},
  verifyArtifactToolImport:true,receiptPath:path.join(work,'v3-validation.json')
 });
 const verified=await PresentationFile.importPptx(await FileBlob.load(finalPath));
 const preview=await verified.slides.items[0].export({format:'png',scale:1});
 await fs.writeFile(path.join(work,'v3-final-render.png'),new Uint8Array(await preview.arrayBuffer()));
 console.log(JSON.stringify({finalPath:result.finalPath,slideCount:1,findings:result.presentationLayout.findingCount}));
}
