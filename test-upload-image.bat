@echo off
echo Testing Image Upload API...

REM Create test image
echo iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg== > temp.txt
certutil -decode temp.txt test.png
del temp.txt

REM Test upload image API
curl -X POST ^
  -F "imageFile=@test.png" ^
  -k ^
  https://localhost:7136/api/Book/test-upload-image

REM Clean up
del test.png

echo Test completed.
