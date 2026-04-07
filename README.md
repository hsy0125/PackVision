# 📦 PackVision

산업용 카메라 영상으로 **포장재의 바코드·유통기한(인쇄)** 영역을 추적·판독하고, 기대값과 비교해 **OK/NOK**를 자동 판정하는 Windows 데스크톱 머신비전 애플리케이션입니다.
<img width="2289" height="1368" alt="image" src="https://github.com/user-attachments/assets/9a7c4222-8966-4820-95d0-8ce8985cc161" />

---

## 🚀 주요 기능

| 영역           | 설명                                                |
| ------------ | ------------------------------------------------- |
| **ROI 추적**   | 포장지가 이동·회전해도 날짜·바코드 영역을 상대 좌표로 유지 (OpenCV CSRT 등) |
| **바코드**      | ROI 크롭 후 ZXing으로 디코딩, 전처리 및 재시도 로직 포함             |
| **유통기한 OCR** | 회전 보정 + 그레이스케일 + Tesseract + 이진화/모폴로지 처리          |
| **검사 판정**    | 바코드/날짜/인쇄(옵션) 일치 여부를 종합하여 OK/NOK 판단               |

---

## ⚙️ 요구 사항

* **OS**: Windows (WinForms 기반)
* **.NET**: `net10.0-windows`
* **카메라 SDK**: Hikrobot MVS SDK (`MvCameraControl.Net.dll`)

### 📂 실행 환경 경로

* **Tesseract 데이터 경로**

  ```
  ./tessdata
  ```

* **카메라 DLL 참조 경로**

  ```
  ..\..\..\dll\MvCameraControl.Net.dll
  ```

> ⚠️ 실행 환경에 따라 DLL 경로는 반드시 확인 및 수정 필요

---

## 📦 의존성 (NuGet)

* OpenCvSharp4 (+ Windows Runtime)
* ZXing.Net (+ Windows 호환 바인딩)
* Tesseract / Tesseract.Drawing
* MvCameraControl.Net (로컬 DLL 포함)

---

## 🛠 빌드 방법

```bash
dotnet build PackVisionApp/PackVisionApp.csproj -c Release
```

---

## 🧠 Tech Stack

### 💻 Core

* C# / .NET WinForms
* 실시간 검사 UI 및 시스템 전체 로직 구현

---

### 🧠 Machine Vision

* OpenCvSharp (OpenCV)

  * 이진화 및 노이즈 제거 기반 전처리
  * Morphology 연산을 통한 텍스트 영역 강화
  * Contour 기반 Blob 검출 (문자 단위 분석)
  * ROI 기반 정밀 영역 검사

---

### 🔍 Recognition

* Tesseract OCR

  * 날짜 및 인쇄 텍스트 인식
  * 전처리 최적화를 통한 인식률 개선

* ZXing

  * 바코드 디코딩 및 값 추출
  * 부분 인식 실패 대응 로직 구현

---

### 📷 Camera Integration

* HikRobot Camera SDK (MvCameraControl)

  * 실시간 프레임 스트리밍 처리
  * Grab 이벤트 기반 검사 트리거 구조 설계
  * 프레임 드롭 방지를 위한 동기 처리 로직 구현

---

### 🏗 System Architecture

* Manager 기반 구조 설계

  * `CameraManager` : 카메라 제어 및 프레임 관리
  * `InspectionManager` : 검사 로직 통합 및 판정 처리
  * `LogManager` : 검사 결과 기록 및 관리

* 모듈 분리 구조

  * Vision / Service / UI 계층 분리
  * 유지보수 및 확장성 고려

---

### 📊 Data & Logging

* log4net

  * 실시간 로그 기록 및 오류 추적

* CSV Logging

  * 검사 결과 자동 저장
  * 1000건 단위 파일 분할 저장 (현장 운영 고려)

---

### 🔧 Collaboration

* Git / GitHub

  * Feature Branch 기반 협업
  * PR 리뷰 및 병합 전략 적용

---

## 💡 Key Achievements

* 실시간 영상 기반 자동 검사 시스템 구현
* 바코드 + 날짜 동시 인식 파이프라인 구축
* ROI 기반 검사로 정확도 및 처리 속도 개선
* OK/NOK 판정 및 Fail Reason 체계화
* 검사 로그 및 이력 관리 기능 구현

---

## 📌 Tesseract OCR 설정 방법

본 프로젝트는 날짜 인식을 위해 **Tesseract OCR**을 사용합니다.
정상 동작을 위해 아래 설정이 반드시 필요합니다.

---

### 1️⃣ 모델 파일 다운로드

Tesseract 언어 데이터 파일 다운로드:

👉 [https://github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata)

다운로드 파일:

```
eng.traineddata
```

---

### 2️⃣ 파일 위치 설정

아래 경로에 파일을 추가해야 합니다:

```
PackVision\PackVisionApp\bin\Debug\net10.0-windows7.0\tessdata
```

> ⚠️ `tessdata` 폴더가 없다면 직접 생성해주세요.

---

### 3️⃣ 최종 폴더 구조

```
PackVisionApp
 └── bin
     └── Debug
         └── net10.0-windows7.0
             └── tessdata
                 └── eng.traineddata
```

---

### 4️⃣ 주의사항

* 해당 파일이 없으면 OCR 기능이 동작하지 않습니다.
* 날짜 인식이 되지 않을 경우 경로를 반드시 확인하세요.


