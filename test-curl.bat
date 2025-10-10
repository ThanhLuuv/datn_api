@echo off
echo Testing Book Creation API with curl...

REM Create test image
echo iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg== > temp.txt
certutil -decode temp.txt test.png
del temp.txt

REM Test API
curl -X POST ^
  -F "isbn=978-604-1-00003-19" ^
  -F "title=Test Book Curl" ^
  -F "categoryId=2" ^
  -F "publisherId=1" ^
  -F "unitPrice=100000" ^
  -F "publishYear=2025" ^
  -F "pageCount=200" ^
  -F "authorIds=1" ^
  -F "imageFile=@test.png" ^
  -k ^
  https://localhost:7136/api/Book

REM Clean up
del test.png

echo Test completed.
