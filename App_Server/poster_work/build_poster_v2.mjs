import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {FileBlob, PresentationFile} from 'file:///C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
import {finalizePresentation} from 'file:///C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations/container_tools/artifact_tool_utils.mjs';

const workspaceDir='C:/Users/hk010/OneDrive/바탕 화면/Full_Server';
const skillDir='C:/Users/hk010/.codex/plugins/cache/openai-primary-runtime/presentations/26.1024.11018/skills/presentations';
const source='C:/Users/hk010/OneDrive/Documents/카카오톡 받은 파일/[Template 12] 전시회 판넬 [C-3조] ANTITUDE_시나리오 기반 AI 투자 판단 학습 플랫폼.pptx';
const work=path.join(workspaceDir,'poster_work');
const finalPath=path.join(workspaceDir,'outputs','ANTITUDE_전시회판넬_앱학습과정반영_수정본.pptx');
const p=await PresentationFile.importPptx(await FileBlob.load(source));
const slide=p.slides.items[0];
const inspection=await p.inspect({select:'$.slides[0].elements[*]',include:['id','name','kind','text','position','style']});
const records=inspection.records;
const rec=n=>records.find(x=>x.name===`Google Shape;${n};p13`);
const find=n=>p.resolve(rec(n).id);
function replace(n,text,size){const s=find(n);s.text=text;if(size)s.text.fontSize=size;return s;}
function shift(n,dy){find(n).position={...rec(n).position,top:rec(n).position.top+dy};}

const goals=replace(99,[
 '투자 입문자가 뉴스와 차트를 해석하고 매매 근거를 설명하는 연습을 할 수 있는 학습 환경 마련',
 '과거 사건을 재현한 시나리오에서 투자 행동과 판단 이유를 함께 기록하며 의사결정 과정 학습',
 '웹의 시세·공시 분석과 모바일 앱의 턴별 실습·AI 비교·복습을 통해 위험 인식과 판단 능력 훈련'
].join('\n'),28);
goals.position={...rec(99).position,height:155.13};
goals.text.style={lineSpacing:1.1};
for(const n of [103,104,105])shift(n,-40);
replace(104,'주요 기능과 앱 학습 경험');
const features=replace(100,[
 '시나리오 실습: 사건·난이도를 선택하고 턴별 뉴스·차트를 확인한 뒤 매매와 판단 근거 제출',
 '턴별 피드백: 잘한 점·놓친 점과 평가 점수를 확인하고, 완료 후 수익률·최종 코칭으로 복습',
 'AI 판단 비교: 종목의 매수·매도·관망 근거·신뢰도를 확인하고 내 판단과 비교, 히스토리 조회',
 '시장 실습: 실시간 차트·호가·뉴스로 가상 주문을 연습하고, 직접 입력한 사건의 시장 반응 분석',
 '학습 관리: 시나리오 이어하기·재도전, 금융 용어·퀴즈, 마이페이지의 자산·진행 상황 확인',
 '웹 제공 기능: 국내·미국 주식 모의투자, 과거 금융 이벤트 시나리오, 공시 RAG·투자자 관점 분석'
].join('\n'),27);
features.position={...rec(100).position,top:673.67,height:240};
features.text.style={lineSpacing:1.05};

replace(110,'앱 학습 흐름과 구성');
replace(101,[
 '클라이언트: Android 앱 Unity·C# / PC 웹 React·TypeScript',
 '서버: 앱 Python·FastAPI(8000 포트 통합), 웹 Node.js·Express / 기록 저장 MongoDB Atlas',
 'AI·데이터: OpenAI·Gemini·Ollama, 공시 RAG·FAISS, KIS Open API, 뉴스·DART·SEC EDGAR'
].join('\n'),26);
replace(102,[
 '가상 자금과 과거 사건을 활용해 금전 손실 없이 투자 판단과 주문 과정을 반복 연습',
 '근거 제출·턴별 코칭·AI 비교·히스토리를 연결하여 판단 과정과 행동의 일관성을 점검',
 '진행 기록과 금융 용어·퀴즈를 활용한 자기주도 복습, 대학 금융·경제 수업 및 입문 교육에 활용'
].join('\n'),27);

// Replace the original web-only architecture with a real learning flow and two client paths.
for(let n=120;n<=165;n++){if(rec(n))p.delete(rec(n).id);}
const blue='#244C7D', ink='#18334E', orange='#AF5D0B';
function txt(name,text,x,y,w,h,size=24,bold=false,color=ink){
 const s=slide.shapes.add({name,geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});
 s.text=text;
 s.text.style={typeface:'Malgun Gothic',fontSize:size,bold,color,alignment:'center',verticalAlignment:'middle',insets:0,lineSpacing:1.05};
 return s;
}
function frame(name,x,y,w,h){return slide.shapes.add({name,geometry:'rect',position:{left:x,top:y,width:w,height:h},fill:'#FFFFFF',line:{fill:'#8C9CAF',width:1.5}});}
function arrow(a,b,kind='straight'){
 const c=slide.shapes.connect(a,b,{kind,fromSide:'right',toSide:'left',line:{style:'solid',fill:'#53697E',width:2},tail:{type:'triangle',width:'med',length:'med'}});
 c.bringToFront();
 return c;
}

txt('app-flow-caption','시나리오에서 판단하고 근거를 남긴 뒤, 피드백을 받아 다시 연습',120,1045,1355,43,27,true,blue);
const steps=[
 ['1  시나리오 선택','사건과 난이도 확인\n진행 중인 학습 이어가기'],
 ['2  정보 확인과 주문','턴별 뉴스·차트 해석\n매수·매도·관망 결정'],
 ['3  판단 근거 제출','선택형 질문과 자유 서술\n이번 턴 주문 내역 확인'],
 ['4  AI 피드백과 복습','턴 점수·개선점 확인\n최종 결과 확인과 재도전']
];
const boxes=[];
for(let i=0;i<4;i++){
 const x=120+i*345;
 const b=frame(`learning-step-${i+1}`,x,1107,305,132);boxes.push(b);
 txt(`learning-title-${i+1}`,steps[i][0],x+8,1114,289,37,26,true,blue);
 txt(`learning-body-${i+1}`,steps[i][1],x+10,1160,285,65,22);
 if(i>0)arrow(boxes[i-1],b);
}

txt('architecture-caption','웹과 앱의 사용 환경에 맞춘 서비스 구성',120,1254,1355,40,27,true,blue);
const web=frame('web-client',125,1322,315,113);
txt('web-client-title','PC 웹',140,1329,285,35,27,true,blue);
txt('web-client-description','React·TypeScript\n국내·미국 주식 모의투자',140,1370,285,57,21);
const mobile=frame('mobile-client',125,1481,315,127);
txt('mobile-client-title','Android 모바일 앱',140,1488,285,34,27,true,blue);
txt('mobile-client-description','Unity·C#\n시나리오·AI 비교·학습 관리',140,1531,285,65,21);

const webserver=frame('web-server',590,1322,350,113);
txt('web-server-title','웹 서비스 서버',603,1329,324,35,26,true,orange);
txt('web-server-description','Node.js·Express\n모의투자·시나리오 서비스',603,1370,324,57,21);
const appserver=frame('app-unified-server',590,1481,350,127);
txt('app-server-title','앱 통합 서버',603,1488,324,34,26,true,orange);
txt('app-server-description','Python·FastAPI / 8000 포트\n로그인·주문·학습·AI 요청',603,1531,324,65,21);
arrow(web,webserver);arrow(mobile,appserver);
txt('web-request','요청·응답',444,1348,142,31,19);
txt('app-request','요청·응답',444,1512,142,31,19);

const common=frame('ai-and-data-services',1110,1322,365,286);
txt('ai-services-title','AI 분석·학습 평가',1120,1330,345,35,26,true,orange);
txt('ai-services-description','OpenAI·Gemini·Ollama\n공시 RAG와 5개 투자자 관점\n규칙 기반 평가와 AI 코칭',1120,1374,345,86,21);
txt('data-services-title','데이터와 기록',1120,1468,345,34,26,true,blue);
txt('data-services-description','KIS 시세·뉴스·공시 API\nMongoDB Atlas\n사용자·주문·학습·판단 기록',1120,1507,345,89,21);
arrow(webserver,common,'elbow');arrow(appserver,common,'elbow');

slide.speakerNotes.append('\n\n앱 학습과정 반영 수정본의 기능 근거:\n'+[
 'Scenario/Play/TurnEndBasisController.cs: 선택형 질문, 자유 근거 입력과 제출',
 'Scenario/Play/ScenarioTurnFeedbackController.cs: 잘한 점, 놓친 점, 턴별 평가와 다음 턴 진행',
 'Scenario/Play/ScenarioFinalResultController.cs: 수익률, 최종 자산, 강점과 개선점, 최종 피드백',
 'Scenario/List/ScenarioProgressListController.cs: 진행 중 이어하기, 완료 시나리오 Info와 재시작 연결',
 'AI_Ramen/AiRamenController.cs: 종목별 AI 판단, 매수·매도·관망 비교, 히스토리 조회',
 'Reaction_Scripts/Reaction_AIChatController.cs: 뉴스·공시·산업·가정 시나리오 입력 후 시장 반응 요청',
 'Term/Quiz/FinanceQuizManager.cs 및 Term/Term/FinanceDictionaryManager.cs: 금융 용어와 퀴즈',
 'MyPage/MyPageController.cs: 자산, 보유 종목, 진행 시나리오 상태 확인',
 '앱 코드 기준 경로: C:/Users/hk010/Ant Teacher_V2/Assets/Scripts/',
 '앱 서버: Full_Server/main.py, start.ps1, services/ramen, services/scenario, services/market_reaction',
 '웹 설명은 사용자가 제공한 원본 판넬을 근거로 유지함. 앱과 웹의 기록이 자동 동기화된다는 주장은 하지 않음.',
 '구성도 오른쪽은 AI·데이터 기능의 개념적 묶음이며 두 프론트엔드의 동일 DB·동일 인증 사용을 뜻하지 않음.'
].join('\n'));

const candidatePath=path.join(work,'candidate-v2.pptx');
await(await PresentationFile.exportPptx(p)).save(candidatePath);
const image=await slide.export({format:'png',scale:1});
await fs.writeFile(path.join(work,'v2-preview.png'),new Uint8Array(await image.arrayBuffer()));
const layout=await slide.export({format:'layout',detail:'full'});
await fs.writeFile(path.join(work,'v2-layout.json'),await layout.text());
const result=await finalizePresentation({
 workspaceDir,candidatePath,finalPath,
 pythonExecutable:'C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 integrityValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_package_integrity.py'),
 layoutValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_layout_geometry.py'),
 layoutArgs:['--expected-slide-size-emu','15083150,21331550','--validate-bullet-geometry','--validate-heading-fit'],
 explicitTotalSlideCount:1,requiredNativeTableOwnerSlides:[],requiredNativeChartOwnerSlides:[],
 fontPolicy:{basis:'reference',families:['Malgun Gothic','Arial','Batang'],referencePath:source,referenceSha256:createHash('sha256').update(await fs.readFile(source)).digest('hex')},
 verifyArtifactToolImport:true,receiptPath:path.join(work,'v2-validation.json')
});
const verified=await PresentationFile.importPptx(await FileBlob.load(finalPath));
const preview=await verified.slides.items[0].export({format:'png',scale:1});
await fs.writeFile(path.join(work,'v2-final-render.png'),new Uint8Array(await preview.arrayBuffer()));
console.log(JSON.stringify({finalPath:result.finalPath,slideCount:1,findings:result.presentationLayout.findingCount}));
