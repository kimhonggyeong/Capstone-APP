import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {FileBlob, PresentationFile} from 'file:///C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
import {finalizePresentation} from 'file:///C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations/container_tools/artifact_tool_utils.mjs';

const workspaceDir='C:/Users/hk010/OneDrive/바탕 화면/Full_Server';
const skillDir='C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations';
const source='C:/Users/hk010/OneDrive/Documents/카카오톡 받은 파일/[Template 12] 전시회 판넬 [C-3조] ANTITUDE_시나리오 기반 AI 투자 판단 학습 플랫폼.pptx';
const work=path.join(workspaceDir,'poster_work');
const finalPath=path.join(workspaceDir,'outputs','ANTITUDE_전시회판넬_웹_앱통합.pptx');
const p=await PresentationFile.importPptx(await FileBlob.load(source));
const slide=p.slides.items[0];
const inspection=await p.inspect({select:'$.slides[0].elements[*]',include:['id','name','kind','text','position','style']});
const records=inspection.records;
const find=n=>{
 const r=records.find(x=>x.name===`Google Shape;${n};p13`);
 if(!r) throw new Error(`Missing reference shape ${n}`);
 return p.resolve(r.id);
};
const replace=(n,text,style={})=>{
 const s=find(n);
 s.text=text;
 s.text.style={...style};
 return s;
};
const original=(n)=>records.find(x=>x.name===`Google Shape;${n};p13`);
const shift=(n,dy)=>{
 const s=find(n);s.position={...original(n).position,top:original(n).position.top+dy};
};

// Preserve the original single-panel size, school branding, headings and artwork.
const goals=replace(99,[
 '투자 초보자가 뉴스와 공시의 핵심 요인·위험을 해석하고 자신의 판단 근거를 점검하도록 지원',
 'PC 웹과 Android 앱에서 실제 시세 모의투자와 과거 금융 이벤트 시나리오를 반복 학습',
 '턴별 투자 행동과 판단 근거를 함께 평가하고 AI 해설로 정보 해석과 위험 인식 연습'
].join('\n'),{fontSize:28,typeface:'Malgun Gothic',lineSpacing:1.1});
goals.position={...original(99).position,height:155.13};
for(const n of [103,104,105])shift(n,-40);

const features=replace(100,[
 '실시간 모의투자: 웹 국내·미국 주식, 앱 국내 주식의 시세·차트 조회와 가상 주문·자산 관리',
 '시나리오 학습: 과거 금융 이벤트의 턴별 뉴스·차트 확인, 주문과 판단 근거 제출',
 '판단 과정 평가: 핵심 요인·정보 해석·위험 인식·행동 정합성·논리적 연결 평가와 AI 코칭',
 'AI 판단 비교: 매수·매도·관망 근거와 신뢰도 확인, 내 판단 비교 및 히스토리 복습',
 '시장 반응 분석: 공시 RAG와 5개 투자자 Agent로 영향 방향·시장 압력·불확실성 제공',
 '모바일 학습 관리: 금융 용어·퀴즈, 마이페이지에서 자산과 시나리오 진행 상황 확인'
].join('\n'),{fontSize:27,typeface:'Malgun Gothic',lineSpacing:1.05});
features.position={...original(100).position,top:673.67,height:240};

replace(101,[
 '개발: 웹 React·TypeScript·Node.js·Express, 앱 Unity·C#, 공통 Python·FastAPI',
 'AI·데이터: OpenAI·Gemini·Ollama, FAISS, KIS Open API, 뉴스·공시, MongoDB Atlas',
 '운영: PC 웹 브라우저·Android 앱, 앱 기능은 FastAPI 통합 서버(8000 포트)로 연결'
].join('\n'),{fontSize:26,typeface:'Malgun Gothic',lineSpacing:1.1});

replace(102,[
 '가상 자금과 과거 시나리오로 실제 금전 손실 없이 투자 판단과 주문 과정을 반복 학습',
 'AI 판단 비교·히스토리와 턴별 코칭으로 근거와 행동의 일관성을 점검하고 취약 영역 복습',
 'PC 웹·모바일 앱을 활용한 금융 입문 교육, 대학 실습 및 자기주도 학습 도구로 활용'
].join('\n'),{fontSize:27,typeface:'Malgun Gothic',lineSpacing:1.1});

// Update existing, editable architecture labels rather than flattening the diagram.
const client=replace(141,'React 웹\nUnity 모바일 앱',{fontSize:25,typeface:'Malgun Gothic',bold:true,lineSpacing:1});
client.position={left:365,top:1221,width:245,height:72};
const clientConnector=find(165);
clientConnector.position={...original(165).position,top:1294,height:59};

replace(123,'웹·앱 서비스 서버',{fontSize:27,typeface:'Malgun Gothic',bold:true});
replace(127,'FastAPI 통합',{fontSize:23,typeface:'Malgun Gothic'});
replace(128,'가상 주문·자산 관리 / 시나리오 진행',{fontSize:21,typeface:'Malgun Gothic'});

const llms=replace(135,'Ollama·OpenAI',{fontSize:20,typeface:'Malgun Gothic'});
llms.position={left:819,top:1478,width:160,height:29};
const ai=replace(137,'공시 RAG·시장 반응·AI 판단 비교\n규칙 기반 채점·AI 코칭·판단 기록',{
 fontSize:24,typeface:'Malgun Gothic',lineSpacing:1.02
});
ai.position={left:690,top:1518,width:420,height:60};

replace(143,'시세·뉴스 / 모의투자\n\n시나리오·턴별 피드백\n\nAI 비교·히스토리\n\n금융 용어·퀴즈 / 마이페이지',{
 fontSize:24,typeface:'Malgun Gothic',lineSpacing:1
});
replace(148,'MongoDB Atlas',{fontSize:23,typeface:'Malgun Gothic'});
const db=replace(149,'사용자·주문\n학습·판단 기록',{fontSize:20,typeface:'Malgun Gothic',lineSpacing:1});
db.position={left:1258,top:1278,width:214,height:46};

const notes=await p.inspect({select:'$.slides[0]',include:['notes.text']});
console.log('NOTES',notes.ndjson);
slide.speakerNotes.text=[notes.records[0]?.notes?.text ?? '', '',
 '앱 기능 출처: Unity 프로젝트 C:/Users/hk010/Ant Teacher_V2/Assets/Scripts/',
 'AI_Ramen/AiRamenController.cs: 종목 AI 분석, 매수·관망·매도 비교, 히스토리 조회',
 'Scenario/Play/TurnEndBasisController.cs 및 ScenarioTurnFeedbackController.cs: 턴별 근거 제출·AI 피드백',
 'Real_Scripts/StockCandlestickLoader.cs: 국내 주식 차트·가상 주문, 차트 드래그와 확대·축소',
 'Term/Quiz/FinanceQuizManager.cs 및 Term/Term/FinanceDictionaryManager.cs: 금융 용어·퀴즈',
 'MyPage/MyPageController.cs: 자산 요약, 보유자산과 시나리오 진행 상황',
 '서버 출처: C:/Users/hk010/OneDrive/바탕 화면/Full_Server/main.py와 services/ramen/config.py',
 '웹 기능과 팀·학교 정보: 사용자가 제공한 원본 판넬',
 '미국 주식은 원본 웹 설명의 범위이며 앱 기능은 국내 주식으로 구분함'
].join('\n');

const img=await slide.export({format:'png',scale:0.9});
await fs.writeFile(path.join(work,'updated-preview.png'),new Uint8Array(await img.arrayBuffer()));
const layout=await slide.export({format:'layout',detail:'full'});
await fs.writeFile(path.join(work,'updated-layout.json'),await layout.text());
const candidatePath=path.join(work,'candidate.pptx');
await(await PresentationFile.exportPptx(p)).save(candidatePath);

const result=await finalizePresentation({
 workspaceDir,candidatePath,finalPath,
 pythonExecutable:'C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 integrityValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_package_integrity.py'),
 layoutValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_layout_geometry.py'),
 layoutArgs:['--expected-slide-size-emu','15083150,21331550','--validate-bullet-geometry','--validate-heading-fit'],
 explicitTotalSlideCount:1,
 requiredNativeTableOwnerSlides:[],requiredNativeChartOwnerSlides:[],
 fontPolicy:{basis:'reference',families:['Malgun Gothic','Arial','Batang'],referencePath:source,referenceSha256:createHash('sha256').update(await fs.readFile(source)).digest('hex')},
 verifyArtifactToolImport:true,
 receiptPath:path.join(work,'final-validation.json')
});
console.log(JSON.stringify(result));
