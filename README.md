# PackVision

산업용 카메라 영상으로 **포장재의 바코드·유통기한(인쇄)** 영역을 추적·판독하고, 기대값과 비교해 **OK/NOK**를 자동 판정하는 Windows 데스크톱 비전 애플리케이션입니다.

## 주요 기능

| 영역 | 설명 |
|------|------|
| **ROI 추적** | 포장지가 이동·회전해도 날짜·바코드 영역을 상대 좌표로 유지 (OpenCV CSRT 등) |
| **바코드** | ROI 크롭 후 ZXing으로 디코딩, 전처리·재시도 포함 |
| **유통기한 OCR** | 회전·그레이스케일·Tesseract 및 이진/모폴로지 경로 |
| **검사 판정** | 바코드/날짜/인쇄(옵션) 일치 여부를 종합해 전체 OK/NOK |

## 요구 사항

- **OS**: Windows (Windows Forms)
- **.NET**: `net10.0-windows` 대상 ([`PackVisionApp.csproj`](PackVisionApp/PackVisionApp.csproj))
- **실행 파일 기준 경로**
  - **Tesseract**: `./tessdata` 폴더에 `eng` 등 학습 데이터 배치 ([`DateReader.cs`](PackVisionApp/Vision/DateReader.cs) 기준)
- **산업용 카메라**: Hikrobot MVS SDK — `MvCameraControl.Net.dll`  
  프로젝트는 상대 경로 `..\..\..\dll\MvCameraControl.Net.dll`을 참조합니다. 빌드/실행 환경에 맞게 DLL 위치를 맞추거나 참조를 수정하세요.

## 의존성 (NuGet)

- OpenCvSharp4 (+ Windows 런타임)
- ZXing.Net (+ Windows 호환 바인딩)
- Tesseract / Tesseract.Drawing
- MvCameraControl.Net (패키지 + 로컬 DLL 참조)

## 빌드

저장소 루트에서 프로젝트 파일을 직접 빌드할 수 있습니다.

```bash
dotnet build PackVisionApp/PackVisionApp.csproj -c Release

#### 📌 Tesseract OCR 설정 방법

본 프로젝트는 날짜 인식을 위해 Tesseract OCR을 사용합니다.
정상 동작을 위해 아래의 설정이 반드시 필요합니다.

1️⃣ 모델 파일 다운로드
Tesseract 언어 데이터 파일 (eng.traineddata) 다운로드
👉 https://github.com/tesseract-ocr/tessdata
2️⃣ 파일 위치 설정

다운로드한 eng.traineddata 파일을 아래 경로에 추가해야 합니다:

PackVision\PackVisionApp\bin\Debug\net10.0-windows7.0\tessdata

⚠️ tessdata 폴더가 없다면 직접 생성해주세요.

3️⃣ 최종 폴더 구조
PackVisionApp
 └── bin
     └── Debug
         └── net10.0-windows7.0
             └── tessdata
                 └── eng.traineddata
4️⃣ 주의사항
해당 파일이 없을 경우 OCR 기능이 동작하지 않습니다.
실행 시 에러가 발생하거나 날짜 인식이 되지 않는 경우, 경로를 반드시 확인하세요.
