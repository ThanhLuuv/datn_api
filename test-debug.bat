@echo off
echo Testing API with debug logging...

echo.
echo 1. Testing basic API endpoint...
curl http://localhost:5256/api/Book/test

echo.
echo.
echo 2. Creating test image...
echo iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg== > temp.txt
certutil -decode temp.txt test.png
del temp.txt

echo.
echo 3. Testing upload image with debug...
curl -X POST -F "imageFile=@test.png" http://localhost:5256/api/Book/test-upload-image

echo.
echo.
echo 4. Testing create book with image (will fail due to auth)...
curl -X POST -F "isbn=978-604-1-00003-25" -F "title=Test Book Debug" -F "categoryId=2" -F "publisherId=1" -F "unitPrice=100000" -F "publishYear=2025" -F "pageCount=200" -F "authorIds=1" -F "imageFile=@test.png" http://localhost:5256/api/Book

REM Clean up
del test.png

echo.
echo Test completed. Check the API console for detailed logs.
