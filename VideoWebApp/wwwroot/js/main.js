document.getElementById('searchBox').addEventListener('keyup', function (e) {
    var searchQuery = e.target.value.toLowerCase();
    var videos = document.querySelectorAll('.video-item');

    videos.forEach(function (video) {
        var title = video.querySelector('.title').textContent.toLowerCase();
        if (title.includes(searchQuery)) {
            video.style.display = '';
        } else {
            video.style.display = 'none';
        }
    });
});
