@echo off
echo Testing Image Upload API with HTTP...

REM Create test image
echo iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg== > temp.txt
certutil -decode temp.txt test.png
del temp.txt

echo.
echo Testing upload image endpoint with HTTP...
curl -X POST -F "imageFile=@test.png" http://localhost:5256/api/Book/test-upload-image

echo.
echo.
echo Testing create book with image using HTTP...
curl -X POST -F "isbn=978-604-1-00003-22" -F "title=Test Book HTTP" -F "categoryId=2" -F "publisherId=1" -F "unitPrice=100000" -F "publishYear=2025" -F "pageCount=200" -F "authorIds=1" -F "imageFile=@test.png" http://localhost:5256/api/Book

REM Clean up
del test.png

echo.
echo Test completed.
